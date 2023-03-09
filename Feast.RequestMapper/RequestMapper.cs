using Feast.RequestMapper.Attribute;
using Feast.RequestMapper.Enum;
using Feast.RequestMapper.Extension;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;


namespace Feast.RequestMapper;
#nullable enable

public static class RequestMapper
{
    internal static readonly Dictionary<Type, Func<StringValues, object?>> Parser = new()
    {
        { typeof(string), v => v.ToString() },
        { typeof(byte), v => { if (byte.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(char), v => { if (char.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(ushort), v => { if (ushort.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(short), v => { if (short.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(uint), v => { if (uint.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(int), v => { if (int.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(ulong), v => { if(ulong.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(long), v => { if(long.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(float), v => { if(float.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(double),v=>{ if(double.TryParse(v, out var val)) { return val; } return null; } },
        { typeof(StringValues), v => v },
        { typeof(List<string>), v => v.ToList() },
        { typeof(IList<string>), v => v.ToList() }
    };

    internal static readonly Dictionary<Registry, HashSet<Type>> RegisteredAttributes = new()
    {
        { Registry.Form , new (){ typeof(FromFormAttribute) } },
        { Registry.Body , new () },
        { Registry.Query , new (){ typeof(FromQueryAttribute) } }
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
    public static TModel Generate<TModel>(this HttpRequest request,bool construct = true) where TModel : class , new()
    {
        var ret = construct ? new() : (TModel)FormatterServices.GetUninitializedObject(typeof(TModel));
        return ret.Map(request);
    }

}

internal static class RequestMapper<TModel> 
{
    static RequestMapper()
    {
        var modelType = typeof(TModel);
        var properties = modelType.GetProperties();
        MappedActions = properties.Select(x =>
        {
            var mapper = GetMapperFromType(x);
            mapper ??= GetMapperFromType(x, modelType);
            return mapper;
        }).SkipWhile(x => x == null);
    }

    private static readonly IEnumerable<Action<TModel,HttpRequest>?> MappedActions;

    #region Query
    private static Action<TModel, HttpRequest> GetQueryMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        if (!RequestMapper.Parser.TryGetValue(info.PropertyType, out var parser))
        {
            throw new ArgumentException(
                $"Attribute " +
                $"should be used on available property type in {nameof(RequestMapper.Parser)}");
        }
        return (model, request) =>
        {
            if (!request.Query.TryGetValue(baseName, out var value))
            {
                if (!request.Query.TryGetValue(adaptedName, out value))
                {
                    return;
                }
            }
            info.SetValue(model, parser(value));
        };
    }
    #endregion

    #region Form
    private static Action<TModel, HttpRequest> GetFormValueMapper(PropertyInfo info)
    {
        var baseName = info.Name;
        var adaptedName = info.Name.AnotherCamelCase();
        if (!RequestMapper.Parser.TryGetValue(info.PropertyType, out var parser))
        {
            throw new ArgumentException(
                $"Attribute " +
                $"should be used on available property type in {nameof(RequestMapper.Parser)}");
        }
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

    /// <summary>
    /// 获取Mapper
    /// </summary>
    /// <param name="info">接收的属性</param>
    /// <param name="type">配置注解的属性</param>
    /// <returns></returns>
    private static Action<TModel, HttpRequest>? GetMapperFromType(PropertyInfo info, MemberInfo? type = null)
    {
        type ??= info;
        if (RequestMapper.HasSpecifiedAttribute(Registry.Form, type))
        {
            return GetFormMapper(info);
        }

        if (RequestMapper.HasSpecifiedAttribute(Registry.Query, type))
        {
            return GetQueryMapper(info);
        }

        return null;
    }

    internal static TModel Map(TModel instance, HttpRequest request)
    {
        MappedActions.ForEach(action => action!(instance, request));
        return instance;
    }
}