BeforeAll {
    $script:ScriptUnderTest = Join-Path $PSScriptRoot '..' 'get-review-threads.ps1'

    if (-not (Get-Command gh -ErrorAction SilentlyContinue))
    {
        function global:gh
        {
            param([Parameter(ValueFromRemainingArguments = $true)]$Args)
        }
    }

    . $ScriptUnderTest -Owner 'intel-agency' -Repo 'DotnetAgents'
}

Describe 'get-review-threads.ps1' {
    BeforeEach {
        $script:SnapshotPath = Join-Path $TestDrive 'snapshot.json'
        . $ScriptUnderTest -Owner 'intel-agency' -Repo 'DotnetAgents' -OutputPath $SnapshotPath
        $script:lastGhArgs = $null
    }

    Context 'Save-ReviewThreadSnapshot' {
        It 'writes the gh response to the configured output path' {
            $ghResponse = '{"data":{"repository":{"name":"DotnetAgents"}}}'
            Mock -CommandName gh -MockWith {
                $script:lastGhArgs = $args
                $ghResponse
            }

            Save-ReviewThreadSnapshot -Number 9

            ((Get-Content -Raw $SnapshotPath).Trim()) | Should -BeExactly $ghResponse
            $lastGhArgs | Should -Not -Be $null
        }
    }

    Context 'Send-ReviewCommentReply' {
        It 'invokes gh with POST, body payload, and in_reply_to reference' {
            $replyBody = "TRest comment. Reply and resolve"
            Mock -CommandName gh -MockWith { $script:lastGhArgs = $args }

            Send-ReviewCommentReply -PullRequestNumber 42 -CommentId 123456 -Body $replyBody

            $lastGhArgs | Should -Contain '-X'
            $lastGhArgs | Should -Contain 'POST'
            $lastGhArgs | Should -Contain "repos/intel-agency/DotnetAgents/pulls/42/comments"
            $lastGhArgs | Should -Contain "body=$replyBody"
            $lastGhArgs | Should -Contain 'in_reply_to=123456'
        }
    }

    Context 'Resolve-ReviewThread' {
        It 'calls gh with the resolveReviewThread mutation' {
            Mock -CommandName gh -MockWith { $script:lastGhArgs = $args }

            Resolve-ReviewThread -ThreadId 'PRRT_test'

            ($lastGhArgs -join ' ') | Should -Match 'resolveReviewThread'
        }
    }
}