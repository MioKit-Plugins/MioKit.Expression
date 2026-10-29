namespace Expression;

public static partial class ExpressionExtension
{
    [EavRelation(typeof(IExpressionPluginFeature))]
    public static SettingEavProperty<bool?> AllowCalculateWithoutAttachProperty { get; } =
        SettingEavPropertyBuilder<bool?>.Create()
            .WithId(ExpressionConst.AllowCalculateWithoutAttachPropertyGuid)
            .WithName("AllowCalculateWithoutAttach")
            .WithDisplayName("全局搜索直接计算")
            .WithDescription("开启后，在全局搜索框输入表达式即可计算；")
            .WithGroup("搜索")
            .WithOrder(10)
            .WithEditor(SettingEditorKind.Boolean)
            .WithDefaultValue(false)
            .Build();
}
