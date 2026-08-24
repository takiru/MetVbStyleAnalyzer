[![NuGet](https://img.shields.io/badge/nuget-v0.0.1-blue.svg)](https://www.nuget.org/packages/MetVbStyleAnalyzer/)

# MetVbStyleAnalyzer

dotnet_diagnostic.MSA1000.severity = error     // 名前空間がフォルダー構造と一致していません  
dotnet_diagnostic.MSA1001.severity = error     // 名前空間が指定されていません  
dotnet_diagnostic.MSA1100.severity = error     // メソッドがプロパティのようにアクセスされています  
dotnet_diagnostic.MSA1101.severity = error     // If / Select Case のネストが深すぎます  
dotnet_diagnostic.MSA1102.severity = error     // ドキュメントコメントが指定されていません  
dotnet_diagnostic.MSA1103.severity = error     // summary の内容が空です  
dotnet_diagnostic.MSA1104.severity = error     // param の記載がありません  
dotnet_diagnostic.MSA1105.severity = error     // param の name 属性が仮引数と一致していません  
dotnet_diagnostic.MSA1106.severity = error     // param の内容が空です  
dotnet_diagnostic.MSA1107.severity = error     // returns が無いか、内容が空です  
dotnet_diagnostic.MSA1108.severity = error     // Public/Protected/Protected Friend の定数に Const を使用しています  
dotnet_diagnostic.MSA1109.severity = error     // Module 名が指定されていません  
dotnet_diagnostic.MSA1110.severity = error     // 型名 (Class/Structure/Interface/Module/Enum/Delegate) がファイル名と一致していません  
msa1106_excluded_param_names = sender,e        // 除外する引数名 (カンマ区切り)  
msa1101_max_nesting_depth = 3                  // 許容するネスト階層数  
msa1107_allow_empty_for_property = true        // プロパティは<returns>が空でも許容する
