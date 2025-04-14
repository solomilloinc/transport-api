namespace Transport.Infraestructure.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AllowAnonymousAttribute : Attribute { }
