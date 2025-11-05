using LoBIM.Services;

namespace LoBIM.Features.Framing.Strategies
{
    public interface IFramingStrategy
    {
        IRevitService RevitService { get; set; }
        IFrameGenerateModel Model { get; set; }
        void StartGenerate();
    }

    public interface IFrameGenerateModel
    {
    }
}
