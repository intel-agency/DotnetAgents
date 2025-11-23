#!/usr/bin/env pwsh

<#!
Helper script to capture PR review thread snapshots via GitHub GraphQL

Usage:
  pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/get-review-threads.ps1 \
    -Owner intel-agency -Repo DotnetAgents -PullRequestNumber 9 \
    -OutputPath .pr-thread-snapshot.json

This wraps the working query format (single-quoted here-string, trimmed, passed
via -f query="…") so future snapshots avoid formatting errors.
!#>

[CmdletBinding()]
param(
  [Parameter()][string]$Owner = 'intel-agency',
  [Parameter()][string]$Repo = 'DotnetAgents',
  [Parameter()][int]$PullRequestNumber,
  [Parameter()][string]$OutputPath = '.pr-thread-snapshot.json',
  [Parameter()][string]$ResolveThreadId,
  [Parameter()][string]$ReplyCommentId,
  [Parameter()][string]$ReplyBody
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Tool
{
  param([Parameter(Mandatory)][string]$Name)
  try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true }
  catch { return $false }
}

if (-not (Test-Tool gh))
{
  throw "GitHub CLI (gh) not found. Install it from https://cli.github.com and run 'gh auth login'."
}

$query = @'
query($owner:String!, $repo:String!, $number:Int!) {
  repository(owner: $owner, name: $repo) {
    name
    pullRequest(number: $number) {
      number
      url
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          isOutdated
          isCollapsed
          path
          comments(first: 100) {
            nodes {
              id
              databaseId
              url
              author { login }
              body
              createdAt
              isMinimized
              minimizedReason
            }
          }
        }
      }
    }
  }
}
'@

$query = $query.Trim()

function Save-ReviewThreadSnapshot
{
  param([Parameter(Mandatory)][int]$Number)

  $ghArgs = @(
    'api', 'graphql',
    '-f', "query=$query",
    '-F', "owner=$Owner",
    '-F', "repo=$Repo",
    '-F', "number=$Number"
  )

  Write-Host "Running: gh $($ghArgs -join ' ')" -ForegroundColor DarkGray
  $result = gh @ghArgs
  if ([string]::IsNullOrWhiteSpace($result))
  {
    throw 'GraphQL query returned no data.'
  }

  $resolvedPath = Resolve-Path -LiteralPath $OutputPath -ErrorAction SilentlyContinue
  if ($null -eq $resolvedPath)
  {
    $resolvedPath = (Resolve-Path -LiteralPath (Split-Path -Parent $OutputPath) -ErrorAction SilentlyContinue)
    if ($null -eq $resolvedPath) { $resolvedPath = Get-Location }
    $resolvedPath = Join-Path $resolvedPath (Split-Path -Leaf $OutputPath)
  }

  $result | Set-Content -Encoding UTF8 -LiteralPath $resolvedPath
  Write-Host "Saved snapshot -> $resolvedPath" -ForegroundColor Cyan
}

function Resolve-ReviewThread
{
  param([Parameter(Mandatory)][string]$ThreadId)

  $mutation = @'
mutation($threadId:ID!) {
  resolveReviewThread(input:{threadId:$threadId}) {
  thread { id isResolved }
  }
}
'@
  $mutation = $mutation.Trim()
  $ghArgs = @(
    'api', 'graphql',
    '-f', "query=$mutation",
    '-f', "threadId=$ThreadId"
  )
  Write-Host "Resolving thread via: gh $($ghArgs -join ' ')" -ForegroundColor DarkGray
  gh @ghArgs | Out-Null
  Write-Host "Resolved thread $ThreadId" -ForegroundColor Green
}

function Send-ReviewCommentReply
{
  param(
    [Parameter(Mandatory)][int]$PullRequestNumber,
    [Parameter(Mandatory)][string]$CommentId,
    [Parameter(Mandatory)][string]$Body
  )

  if ([string]::IsNullOrWhiteSpace($Body))
  {
    throw 'Reply body cannot be empty.'
  }

  $ghArgs = @(
    'api', '-X', 'POST',
    "repos/$Owner/$Repo/pulls/$PullRequestNumber/comments",
    '-f', "body=$Body",
    '-F', "in_reply_to=$CommentId"
  )
  Write-Host "Replying via: gh $($ghArgs -join ' ')" -ForegroundColor DarkGray
  gh @ghArgs | Out-Null
  Write-Host "Posted reply to comment $CommentId" -ForegroundColor Green
}

if ($PSBoundParameters.ContainsKey('PullRequestNumber'))
{
  Save-ReviewThreadSnapshot -Number $PullRequestNumber
}

if ($PSBoundParameters.ContainsKey('ReplyCommentId'))
{
  if (-not $PSBoundParameters.ContainsKey('PullRequestNumber'))
  {
    throw 'PullRequestNumber is required when sending a reply.'
  }

  Send-ReviewCommentReply -PullRequestNumber $PullRequestNumber -CommentId $ReplyCommentId -Body $ReplyBody
}

if ($PSBoundParameters.ContainsKey('ResolveThreadId'))
{
  Resolve-ReviewThread -ThreadId $ResolveThreadId
}