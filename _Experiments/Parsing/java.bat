@chcp 1251
".\PipelineTools\Split\bin\Debug\Split.exe" ".\_Results\java" --change class_struct_interface inner_class_struct_interface
".\Baselines\JavaAntlrBaseline\bin\Debug\JavaAntlrBaseline.exe" ".\_Results\java"
".\PipelineTools\RemoveMatches\bin\Debug\RemoveMatches.exe" ".\_Results\java"
pause