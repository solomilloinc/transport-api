namespace transport.infraestructure.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AllowAnonymousAttribute : Attribute { }
