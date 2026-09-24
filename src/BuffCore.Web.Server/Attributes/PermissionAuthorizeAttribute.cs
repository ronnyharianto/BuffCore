namespace BuffCore.Web.Server.Attributes
{
    /// <summary>
    /// Specifies one or more required permissions for accessing a controller action.
    /// Use this attribute to restrict access based on defined permission strings.
    /// <para>Example: [PermissionAuthorize("User.Read", "User.Write")]</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PermissionAuthorizeAttribute(params string[] permissions) : Attribute
    {
        /// <summary>
        /// Gets the list of permissions required to attribute-decorated actions.
        /// </summary>
        public string[] Permissions { get; } = permissions;
    }

    /// <summary>
    /// Use this attribute to mark a controller action as accessible by API key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PermissionAuthorizeApiKeyAttribute() : Attribute
    {
    }
}
