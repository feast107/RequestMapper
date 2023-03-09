# RequestMapper
通过注解自动映射到实例的类库，适用于请求结构复杂，传统的参数注解无法映射的情况

## Effect
![](./doc/Example.png)

## Start
+ 在需要映射的模型上添加注解，内置提供了一部分，他们在命名空间 `Feast.RequestMapper.Attribute` 下
    
    ```CSharp
    [FromQuery]
    public class Model 
    {
        public string Id { get; set; }
        public string? Name { get; init; }

        [FromForm]
        public IFormFile Logo { get; init; }
        [FromForm]
        public IReadOnlyList<IFormFile> Pictures { get; init; }
    }
    ```
+ 如果需要使用自定义注解或者内置注解，可以通过注册
  
    ```CSharp
    RequestMapper.RegisterAttribute<YourAttribute>(Registry.AsYourWish);
    ```
+ 通过请求的报文来生成

    ```CSharp
    var model = new Model();
    var newModel = RequestMappper.Generate(this.Request);
    model.Map(this.Request);
    ```

## Preview
+ :construction: 映射处理系统（真的需要吗）