using Feast.RequestMapper.Enum;
using Feast.RequestMapper.Extension;
using Feast.RequestMapper.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Feast.RequestMapper;
#nullable enable
public static class RequestMapper
{
    internal class NoSerializer : ISerializer { public object? Deserialize(string input, Type returnType) => null; }

    public static Encoding Encoding { get; set; } = Encoding.UTF8;
    
    public static ISerializer Serializer { get; set; } = new NoSerializer();

    internal static readonly Dictionary<Type, Func<StringValues, object?>> Parser = new()
    {
        { typeof(string), v => v.ToString() },
        { typeof(byte),   v => { if (byte.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(char),   v => { if (char.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(ushort), v => { if (ushort.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(short),  v => { if (short.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(uint),   v => { if (uint.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(int),    v => { if (int.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(ulong),  v => { if (ulong.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(long),   v => { if (long.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(float),  v => { if (float.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(double), v => { if (double.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(StringValues), v => v },
        { typeof(List<string>), v => v.ToList() },
        { typeof(IList<string>), v => v.ToList() }
    };

    private static readonly Dictionary<Registry, HashSet<Type>> RegisteredAttributes = new()
    {
        { Registry.Header, new() { typeof(FromHeaderAttribute) } },
        { Registry.Form, new() { typeof(FromFormAttribute) } },
        { Registry.Body, new() { typeof(FromBodyAttribute) } },
        { Registry.Query, new() { typeof(FromQueryAttribute) } }
    };

    internal static bool HasSpecifiedAttribute(Registry key, MemberInfo info) => RegisteredAttributes[key].Any(x => info.GetCustomAttribute(x) != null);

    /// <summary>
    /// 注册自定义注解
    /// </summary>
    /// <typeparam name="TAttribute">注解类型</typeparam>
    /// <param name="key">从<see cref="HttpRequest"/>中读取的域</param>
    public static void RegisterAttribute<TAttribute>(Registry key) where TAttribute : System.Attribute => RegisteredAttributes[key].Add(typeof(TAttribute));
    /// <summary>
    /// 注册自定义注解
    /// </summary>
    /// <param name="key">从<see cref="HttpRequest"/>中读取的域</param>
    /// <param name="attributeType">注解类型</param>
    public static void RegisterAttribute(Registry key, Type attributeType)
    {
        if (attributeType.IsSubclassOf(typeof(System.Attribute))) RegisteredAttributes[key].Add(attributeType);
        else throw new ArgumentException($"{attributeType} should be derived from {typeof(System.Attribute)}");
    }

    /// <summary>
    /// 映射到目标对象
    /// </summary>
    /// <typeparam name="TModel">目标类型</typeparam>
    /// <param name="instance">对象</param>
    /// <param name="request">报文</param>
    /// <returns></returns>
    public static TModel Map<TModel>(this TModel instance, HttpRequest request) where TModel : class => RequestMapper<TModel>.Map(instance, request);

    /// <summary>
    /// 创建并映射到目标对象
    /// </summary>
    /// <typeparam name="TModel">对象</typeparam>
    /// <param name="request">报文</param>
    /// <param name="construct">是否调用构造</param>
    /// <returns></returns>
    public static TModel Generate<TModel>(this HttpRequest request, bool construct = true)
        where TModel : class, new() =>
        construct ? new() : (TModel)FormatterServices.GetUninitializedObject(typeof(TModel)).Map(request);
}

internal static class RequestMapper<TModel> 
{
    static RequestMapper()
    {
        var modelType = typeof(TModel);
        var properties = modelType.GetProperties();
        MappedActions = properties
            .Where(x=>x.CanWrite)
            .Select(x => GetMapperFromType(x, modelType))
            .Where(x => x != null);
    }

    private static readonly IEnumerable<Action<TModel,HttpRequest>?> MappedActions;

    private static Func<StringValues, object?> TryGetParser(Type inputType)
    {
        if (!RequestMapper.Parser.TryGetValue(inputType, out var parser))
        {
            throw new ArgumentException(
                $"Attribute " +
                $"should be used on available property type in {nameof(RequestMapper.Parser)}");
        }
        return parser;
    }

    #region Header

    private static Action<TModel, HttpRequest> GetHeaderMapper(PropertyInfo info)
    {
        var name = info.Name;
        var another = info.Name.AnotherCamelCase();
        var parser = TryGetParser(info.PropertyType);
        return (model, request) =>
        {
            if (request.Headers.TryGetValue(name, out var str)
                || request.Headers.TryGetValue(another, out str))
                info.SetValue(model, parser(str));
        };
    }

    #endregion

    #region Query
    private static Action<TModel, HttpRequest> GetQueryMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        var parser = TryGetParser(info.PropertyType);
        return (model, request) =>
        {
            if (request.Query.TryGetValue(baseName, out var value)
                || request.Query.TryGetValue(adaptedName, out value))
            {
                info.SetValue(model, parser(value));
            }
        };
    }
    #endregion

    #region Form
    private static Action<TModel, HttpRequest> GetFormValueMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        var parser = TryGetParser(info.PropertyType);
        return (model, request) =>
        {
            if (!request.Form.TryGetValue(baseName, out var value))
            {
                if (!request.Form.TryGetValue(adaptedName, out value))
                {
                    return;
                }
            }

            info.SetValue(model, parser(value));
        };
    }
    private static Action<TModel, HttpRequest> GetFormFileMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        return (model, request) =>
        {
            var file = request.Form.Files.GetFile(baseName); 
            file ??= request.Form.Files.GetFile(adaptedName);
            info.SetValue(model, file);
        };
    }
    private static Action<TModel, HttpRequest> GetFormFilesMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        return (model, request) => 
        {
            var files = request.Form.Files.GetFiles(baseName);
            if(files.Count == 0 ) files = request.Form.Files.GetFiles(adaptedName);
            info.SetValue(model, files.Count == 0 ? null : files);
        };
    }
    private static Action<TModel, HttpRequest> GetFormMapper(PropertyInfo info)
    {
        if(info.PropertyType == typeof(IFormFile))
        {
            return GetFormFileMapper(info);
        }

        if (info.PropertyType == typeof(IReadOnlyList<IFormFile>))
        {
            return GetFormFilesMapper(info);
        }
        return GetFormValueMapper(info);

    }
    #endregion

    #region Body

    private static Action<TModel, HttpRequest> GetBodyMapper(PropertyInfo info)
    {
        string ReadStream(Stream stream)
        {
            if (!stream.CanRead) return string.Empty;
            if (stream.Length > int.MaxValue)
            {
                throw new OverflowException("Body Stream length exceed");
            }

            var length = (int)stream.Length;
            var bytes = new byte[length];
            return stream.Read(bytes, 0, length) == length
                ? RequestMapper.Encoding.GetString(bytes)
                : string.Empty;
        }

        return RequestMapper.Parser.TryGetValue(info.PropertyType, out var parser)
            ? (model, request) => info.SetValue(model, parser.Invoke(ReadStream(request.Body)))
            : (model, request) => info.SetValue(model, RequestMapper.Serializer.Deserialize(ReadStream(request.Body), info.PropertyType));
    }

    #endregion

    /// <summary>
    /// 获取Mapper
    /// </summary>
    /// <param name="info">接收的属性</param>
    /// <param name="type">配置注解的属性</param>
    /// <returns></returns>
    private static Action<TModel, HttpRequest>? GetMapperFromType(PropertyInfo info, MemberInfo? type = null)
    {
        type ??= info;
        return RequestMapper.HasSpecifiedAttribute(Registry.Header, type) ? GetHeaderMapper(info)
            : RequestMapper.HasSpecifiedAttribute(Registry.Form, type) ? GetFormMapper(info)
            : RequestMapper.HasSpecifiedAttribute(Registry.Query, type) ? GetQueryMapper(info)
            : RequestMapper.HasSpecifiedAttribute(Registry.Body, info) ? GetBodyMapper(info)
            : null;
    }

    internal static TModel Map(TModel instance, HttpRequest request) => 
        MappedActions
            .Aggregate(instance, (i, a) => 
                { a?.Invoke(i, request); return i; });
}