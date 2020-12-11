using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
namespace InspectionNew
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Buildalyzerのルート
            var manager = new AnalyzerManager();
 
            // slnを指定してまるっと持ってくるでもいいんですが、とりあえずcsproj単品で並べてみた。
            // 解析対象となる対象のプロジェクトと、参照としてのUniRxのプロジェクトを登録(Getすると同時に登録も行われるという仕様……)
            manager.GetProject(@"C:\***\UniRx.csproj");
            manager.GetProject(@"C:\***\UniRx.Async.csproj");
            manager.GetProject(@"C:\***\Assembly-CSharp.csproj");
 
            // 登録されたプロジェクト群からAdhocWorkspaceを作成
            var workspace = manager.GetWorkspace();
            var targetProject = workspace.CurrentSolution.Projects.First(x => x.Name == "Assembly-CSharp");
 
            // コンパイル結果みたいなのを取得
            var compilation = await targetProject.GetCompilationAsync();
 
            // 比較対象の型シンボルをCompilationから持ってくる
            var observableSymbol = compilation.GetTypeByMetadataName("UniRx.Observable");
 
            // 収集
            var methods = new List<IMethodSymbol>();
            foreach (var tree in compilation.SyntaxTrees)
            {
                // Syntax -> Symbol変換するためのModelの取得
                var semanticModel = compilation.GetSemanticModel(tree);
 
                // シンタックスツリーからメソッド呼び出しだけを抜き出し
                var invocationExpressions = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>();
 
                foreach (var expr in invocationExpressions)
                {
                    // その中からUniRx.Observableに属するメソッドのみを抽出
                    var methodSymbol = semanticModel.GetSymbolInfo(expr).Symbol as IMethodSymbol;
                    if (methodSymbol?.ContainingType == observableSymbol)
                    {
                        methods.Add(methodSymbol);
                    }
                }
            }
 
            // 解析
            Console.WriteLine("## TotalCount:" + methods.Count);
 
            var grouping = methods
                .Select(x => x.OriginalDefinition) // Foo -> Fooへ変換
                .Select(x => x.Name) // 今回はメソッド名だけ取る(オーバーロードの違いは無視)
                .GroupBy(x => x)
                .OrderByDescending(x => x.Count())
                .Select(x => new { MethodName = x.Key, Count = x.Count() })
                .ToArray();
 
            // マークダウンの表で出力
            Console.WriteLine("| メソッド名| 呼び出し数 |");
            Console.WriteLine("| --- | --- |");
            foreach (var item in grouping)
            {
                Console.WriteLine($"| {item.MethodName} | {item.Count} |");
            }
        }
    }
}