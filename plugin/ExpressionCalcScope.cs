namespace Expression;

[EavType(ExpressionConst.CalcScopeTypeId)]
public sealed class ExpressionCalcScope : SearchScopeFeatureBase, IMemoryNodeFeature
{
    public ExpressionCalcScope(string id) : base(id)
    {
        Name = "表达式计算";
        Description = "输入数学表达式并复制计算结果";
    }

    public override Guid MioType => ExpressionConst.CalcScopeType;

    public override IReadOnlyList<string> SearchCommands { get; } = ["calc", "expression"];

    protected override IEnumerable<ISearchableFeature> GetSearchableFeatures()
    {
        var node = MioAppContext.Current.RootNode
            .GetNodeById<ExpressionCalcNode>(ExpressionConst.CalcNodeId);
        if (node != null)
        {
            yield return node;
        }
    }
}
