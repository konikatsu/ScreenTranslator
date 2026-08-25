# AIツール・CLI 連携ガイド

当プロジェクトにおいて、エージェントや自動化スクリプトがコードレビューや生成AIツールを利用する際の注意点をまとめる。

## Codex CLI (GitHub Copilot CLI) の非対話・自動化利用について

*   **事象**: コマンドラインツール等からパイプライン経由で Get-Content diff.txt | codex ... と実行すると、Error: stdin is not a terminal となりクラッシュする。
*   **原因**: codex コマンド単体は対話型インターフェース (TUI) を起動するため、非対話環境（パイプやバックグラウンド）からの入力を受け付けない。
*   **対策**: スクリプトやCI、自動化エージェントから利用する場合は、必ず非対話用のサブコマンドである codex exec または codex review を使用すること。
    *   コードの実行や一般的な指示: codex exec
        (例: Get-Content diff.txt -Raw | codex exec "このdiffをレビューして")
    *   コードレビュー専用: codex review
        (例: codex review --uncommitted や codex review --base main)

## Claude Code CLI の非対話・自動化利用について

*   **事象**: claude コマンド単体も対話型インターフェース (TUI) として起動するため、スクリプト等から無計画に呼び出すと入力待ちでブロックしたり、環境によってはエラーになる可能性がある。
*   **対策**: スクリプトやエージェントからワンショットの処理（コードレビューやテキスト解析など）を依頼する場合は、必ず -p または --print オプションを使用し、非対話モードで実行すること。
    *   実行例: Get-Content diff.txt | claude -p "このdiffをレビューして"
    *   モデル変更例: claude --model claude-fable-5 -p "プロンプト"
*   **特徴**: -p オプションを付与することで、標準入力（パイプ）からのコンテキストを読み取り、指定したプロンプトに対する回答のみを標準出力（stdout）へ返し、直ちに終了する。これによりバックグラウンドタスクやCI環境でも安全に利用できる。
