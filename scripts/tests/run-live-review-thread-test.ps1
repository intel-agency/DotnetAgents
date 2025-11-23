#!/usr/bin/env pwsh

<#!
Runs an end-to-end validation of scripts/get-review-threads.ps1 against the
fake draft PR (#10) by creating a temporary review comment, invoking the script
to reply + resolve, and then confirming the thread is marked resolved.
!#>

[CmdletBinding()]
param(
    [string]$Owner = 'intel-agency',
    [string]$Repo = 'DotnetAgents',
    [int]$PullRequestNumber = 10,
    [string]$TargetPath = 'fixtures/fake-pr-fixture.md',
    [string]$ReplyTemplate = '✅ Harness reply for comment {0}',
    [switch]$CleanupComment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Tool
{
    param([Parameter(Mandatory)][string]$Name)
    try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false }
}

function Get-ReviewThreadIdForComment
{
    param(
        [Parameter(Mandatory)][string]$Owner,
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][int]$PullRequestNumber,
        [Parameter(Mandatory)][long]$CommentDatabaseId
    )

    $threadLookupQuery = @'
query($owner:String!, $repo:String!, $number:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$number) {
      reviewThreads(first:100) {
        nodes {
          id
          comments(first:100) {
            nodes { databaseId }
          }
        }
      }
    }
  }
}
'@.Trim()

    $resp = gh api graphql -f "query=$threadLookupQuery" -f owner=$Owner -f repo=$Repo -F number=$PullRequestNumber | ConvertFrom-Json
    $threads = $resp.data.repository.pullRequest.reviewThreads.nodes
    foreach ($thread in $threads)
    {
        foreach ($comment in $thread.comments.nodes)
        {
            if ($comment.databaseId -eq $CommentDatabaseId)
            {
                return $thread.id
            }
        }
    }

    throw "Could not locate review thread for comment $CommentDatabaseId."
}

if (-not (Test-Tool gh))
{
    throw "GitHub CLI (gh) is required. Install from https://cli.github.com and run 'gh auth login'."
}

Write-Host "[1/5] Fetching PR metadata" -ForegroundColor Cyan
$prInfoQuery = @'
query($owner:String!, $repo:String!, $number:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$number) {
      number
      url
      headRefOid
    }
  }
}
'@.Trim()
$prInfoResp = gh api graphql -f "query=$prInfoQuery" -f owner=$Owner -f repo=$Repo -F number=$PullRequestNumber | ConvertFrom-Json
$prData = $prInfoResp.data.repository.pullRequest
$commitId = $prData.headRefOid
if ([string]::IsNullOrWhiteSpace($commitId))
{
    throw "Unable to determine head commit for PR #$PullRequestNumber."
}

$harnessBody = "Harness test comment $(Get-Date -Format o)"
Write-Host "[2/5] Creating test review comment" -ForegroundColor Cyan
$commentResponse = gh api "repos/$Owner/$Repo/pulls/$PullRequestNumber/comments" `
    -f "body=$harnessBody" `
    -f "commit_id=$commitId" `
    -f "path=$TargetPath" `
    -F line=1 `
    -F side=RIGHT | ConvertFrom-Json

$commentId = $commentResponse.id
$commentNodeId = $commentResponse.node_id
if (-not $commentId -or -not $commentNodeId)
{
    throw 'Failed to create review comment for harness test.'
}
Write-Host "    Created comment #$commentId" -ForegroundColor DarkGray

Write-Host "[3/5] Resolving thread metadata" -ForegroundColor Cyan
$threadId = Get-ReviewThreadIdForComment -Owner $Owner -Repo $Repo -PullRequestNumber $PullRequestNumber -CommentDatabaseId $commentId

$replyBody = [string]::Format($ReplyTemplate, $commentId)
Write-Host "[4/5] Running get-review-threads.ps1 to reply & resolve" -ForegroundColor Cyan
$scriptCmd = @(
    'pwsh', '-NoProfile', '-ExecutionPolicy', 'Bypass',
    '-File', 'scripts/get-review-threads.ps1',
    '-Owner', $Owner,
    '-Repo', $Repo,
    '-PullRequestNumber', $PullRequestNumber,
    '-ReplyCommentId', $commentId,
    '-ReplyBody', $replyBody,
    '-ResolveThreadId', $threadId
)
Write-Host "    > $($scriptCmd -join ' ')" -ForegroundColor DarkGray
$pwshExe = $scriptCmd[0]
$pwshArgs = $scriptCmd[1..($scriptCmd.Length - 1)]
& $pwshExe @pwshArgs

$verifyQuery = @'
query($id:ID!) {
  node(id:$id) {
    ... on PullRequestReviewThread {
      id
      isResolved
      comments(last:5) {
        nodes {
          body
          author { login }
        }
      }
    }
  }
}
'@
$verifyQuery = $verifyQuery.Trim()

Write-Host "[5/5] Verifying thread resolution" -ForegroundColor Cyan
$maxAttempts = 10
$delaySeconds = 3
$threadNode = $null
$replyMatch = $null
$expectedReplySnippet = "Harness reply for comment $commentId"

for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
{
    Write-Host "    Attempt $attempt/$maxAttempts" -ForegroundColor DarkGray

    if ($attempt -gt 1)
    {
        Start-Sleep -Seconds $delaySeconds
    }

    $verifyResp = gh api graphql -f "query=$verifyQuery" -f "id=$threadId" | ConvertFrom-Json
    $threadNode = $verifyResp.data.node
    if (-not $threadNode)
    {
        continue
    }

    $replyMatch = $threadNode.comments.nodes | Where-Object { $_.body -like "*$expectedReplySnippet*" }
    if ($threadNode.isResolved -and $replyMatch)
    {
        break
    }
}

if (-not $threadNode -or -not $threadNode.isResolved)
{
    throw 'Thread is not resolved after running get-review-threads.ps1'
}

if (-not $replyMatch)
{
    throw 'Thread comments do not contain the expected reply body.'
}

Write-Host "Success: thread $threadId resolved with reply from $($replyMatch[-1].author.login)." -ForegroundColor Green

if ($CleanupComment)
{
    Write-Host 'Cleaning up test comment' -ForegroundColor DarkGray
    gh api -X DELETE "repos/$Owner/$Repo/pulls/comments/$commentId" | Out-Null
}