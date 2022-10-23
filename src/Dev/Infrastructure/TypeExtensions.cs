namespace Dev.Controllers.Github.Internal;

using k8s.Models;

public static class TypeExtensions
{
    public static KubernetesEntityAttribute? GetCrdMeta(this System.Type type)
    {
        return type.GetAttr<KubernetesEntityAttribute>();
    }
    public static T? GetAttr<T>(this System.Type type) where T : Attribute
    {
        var entityAttrType = typeof(T);
        var attr = type
            .GetCustomAttributes(entityAttrType, false)
            .Cast<T>()
            .FirstOrDefault();

        return attr;
    }
}