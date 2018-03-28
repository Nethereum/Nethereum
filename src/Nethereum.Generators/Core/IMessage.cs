namespace Nethereum.Generators.Core
{
    public interface IMessage<TParameter> where TParameter : Parameter
    {
        TParameter[] InputParameters { get; }
        string Name { get; set; }
    }
}