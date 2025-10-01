using System;

namespace System.ServiceModel
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public sealed class ServiceContractAttribute : Attribute
    {
        public string Namespace { get; set; }
        public string Name { get; set; }
        public SessionMode SessionMode { get; set; } = SessionMode.Allowed;
        public bool CallbackContract { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OperationContractAttribute : Attribute
    {
        public bool IsOneWay { get; set; }
        public string Action { get; set; }
        public string ReplyAction { get; set; }
    }

    public enum SessionMode
    {
        Allowed,
        NotAllowed,
        Required
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class DataContractAttribute : Attribute
    {
        public string Namespace { get; set; }
        public string Name { get; set; }
        public bool IsReference { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DataMemberAttribute : Attribute
    {
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public bool EmitDefaultValue { get; set; } = true;
        public string Name { get; set; }
    }
}

namespace System.ServiceModel.Web
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WebGetAttribute : Attribute
    {
        public string UriTemplate { get; set; }
        public string ResponseFormat { get; set; }
        public string RequestFormat { get; set; }
        public bool BodyStyle { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WebInvokeAttribute : Attribute
    {
        public string Method { get; set; }
        public string UriTemplate { get; set; }
        public string ResponseFormat { get; set; }
        public string RequestFormat { get; set; }
        public bool BodyStyle { get; set; }
    }
}
