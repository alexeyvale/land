@chcp 1251
".\PipelineTools\Split\bin\Debug\Split.exe" ".\_Results\sharp" --break field 1
".\Baselines\SharpRoslynBaseline\bin\Release\SharpRoslynBaseline.exe" ".\_Results\sharp"
".\PipelineTools\RemoveMatches\bin\Debug\RemoveMatches.exe" ".\_Results\sharp"
pause