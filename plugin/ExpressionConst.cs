namespace Expression;

public static class ExpressionConst
{
    public const string PluginId = "miokit.expression";

    // These identifiers are part of the persisted EAV contract and must remain
    // stable across the migration from the pre-1.0 plugin.
    public const string PluginTypeId = "52A06C67-516F-4B93-B71E-611265DE359D";
    public static readonly Guid PluginType = Guid.Parse(PluginTypeId);

    public const string CalcScopeId = "expression-calc-scope";
    public const string CalcScopeTypeId = "8C9D49F8-BC08-4EE0-97B7-14F64F947987";
    public static readonly Guid CalcScopeType = Guid.Parse(CalcScopeTypeId);

    public const string CalcNodeId = "expression-calc-node";
    public const string CalcNodeTypeId = "8F78763B-8263-4557-B396-37781645C6C7";
    public static readonly Guid CalcNodeType = Guid.Parse(CalcNodeTypeId);

    public const string AllowCalculateWithoutAttachPropertyId = "947C3FB5-D912-4F15-9812-8DD9CD4102FA";
    public static readonly Guid AllowCalculateWithoutAttachPropertyGuid =
        Guid.Parse(AllowCalculateWithoutAttachPropertyId);
}
