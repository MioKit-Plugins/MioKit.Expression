using Expression.Services;
using Expression.Interfaces;
using Microsoft.ClearScript.V8;

namespace Expression;

public class ExpressionRegister : RegisterBase<ExpressionRegister>
{
    public override void RegisterService(ContainerBuilder builder, IServiceCollection services)
    {
        var plugin = new ExpressionPlugin();

        builder.RegisterInstance(plugin)
            .As<IPlugin>()
            .Keyed<IPlugin>(ExpressionConst.PluginId)
            .As(plugin.GetType());

        builder.Register(_ => new V8Runtime()).SingleInstance();
        builder.RegisterType<ParameterResolver>().As<IParameterResolver>().SingleInstance();
        builder.RegisterType<ExpressionParser>().As<IExpressionParser>().SingleInstance();
        builder.RegisterType<ScriptEngineFactory>().As<IScriptEngineFactory>().SingleInstance();
        builder.RegisterType<SearchExpressionEvaluator>().AsSelf().SingleInstance();
        builder.RegisterType<ExpressionCalcScope>().AsSelf().InstancePerDependency();
        builder.RegisterType<ExpressionCalcNode>().AsSelf().InstancePerDependency();
        builder.RegisterType<ExpressionScriptLifecycle>()
            .AsSelf()
            .As<IPluginLifecycleComponent>()
            .SingleInstance();

        services.AddSingleton<ExpressionJsService>();
        services.AddSingleton<HotKeyCaptureJsService>();
    }
}
