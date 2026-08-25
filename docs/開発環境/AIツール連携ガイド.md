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
