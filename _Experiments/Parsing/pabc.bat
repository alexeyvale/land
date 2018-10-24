@chcp 1251
".\PipelineTools\Split\bin\Debug\Split.exe" ".\_Results\pabc" --change routine routine_header
".\Baselines\PascalAbcBaseline\bin\Debug\PascalAbcBaseline.exe" ".\_Results\pabc"
".\PipelineTools\RemoveMatches\bin\Debug\RemoveMatches.exe" ".\_Results\pabc"
pause