using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;

namespace SwarmExtensions.Spectrum;

public class Spectrum : Extension
{
    public static T2IRegisteredParam<float> SpectrumWParam;
    public static T2IRegisteredParam<int> SpectrumMParam;
    public static T2IRegisteredParam<float> SpectrumLamParam;
    public static T2IRegisteredParam<int> SpectrumWindowSizeParam;
    public static T2IRegisteredParam<float> SpectrumFlexWindowParam;
    public static T2IRegisteredParam<int> SpectrumWarmupStepsParam;
    public static T2IRegisteredParam<int> SpectrumStopCachingStepParam;
    public static T2IRegisteredParam<int> SpectrumStepsParam;

    public static T2IParamGroup SpectrumParamGroup;

    public override void OnInit()
    {
        InstallableFeatures.RegisterInstallableFeature(
            new(
                "spectrum",
                "spectrum",
                "https://github.com/ruwwww/comfyui-spectrum-sdxl",
                "ruwwww",
                AutoInstall: true
            )
        );

        SpectrumParamGroup = new("Spectrum", Toggles: true, Open: false, IsAdvanced: false);

        SpectrumWParam = T2IParamTypes.Register<float>(
            new(
                "Spectrum - w",
                "Blending weight between predicted and last true features. Lower values (0.4-0.5) rely more on local momentum, preserving sharpness, while higher values rely on global spectral smoothing. setting w to 0 means using Local Taylor based approximation effectively ignoring the global smoothing parameters (m and lam).",
                "0.3",
                ViewMax: 1,
                Max: 1,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 1
            )
        );

        SpectrumMParam = T2IParamTypes.Register<int>(
            new(
                "Spectrum - m",
                "Number of Chebyshev polynomial basis functions (forecast complexity). Lower values (3) are generally more stable for short SDXL runs.",
                "3",
                ViewMin: 1,
                ViewMax: 8,
                Min: 1,
                Max: 8,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 2
            )
        );

        SpectrumLamParam = T2IParamTypes.Register<float>(
            new(
                "Spectrum - lam",
                "Ridge regularization strength. High values (1.0) prevent latent explosion, rainbow artifacts, and black outputs in low-precision modes.",
                "0.1",
                ViewMax: 2,
                Max: 2,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 3
            )
        );

        SpectrumWindowSizeParam = T2IParamTypes.Register<int>(
            new(
                "Spectrum - window size",
                "Initial forecasting window size (number of skipped steps).",
                "2",
                ViewMin: 1,
                ViewMax: 10,
                Min: 1,
                Max: 10,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 4
            )
        );

        SpectrumFlexWindowParam = T2IParamTypes.Register<float>(
            new(
                "Spectrum - flex window",
                "Increment added to the window after each actual UNet pass. Higher values result in aggressive acceleration.",
                "0.25",
                ViewMax: 2,
                Max: 2,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 5
            )
        );

        SpectrumWarmupStepsParam = T2IParamTypes.Register<int>(
            new(
                "Spectrum - warmup steps",
                "Number of initial full-model steps before forecasting begins. Gives the model time to establish composition. Recommended value: SDXL models = 6, DiT models = 8~10.",
                "6",
                ViewMax: 20,
                Max: 20,
                Toggleable: false,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 6
            )
        );

        SpectrumStopCachingStepParam = T2IParamTypes.Register<int>(
            new(
                "Spectrum - stop caching step",
                "The exact step count where Spectrum stops accelerating and hands rendering back to the native UNet. Essential for recovering fine details. (e.g., If rendering 25 total steps, set to 22 to let the original UNet render the final 3 steps). Set to 100 to disable the guard for maximum speed. Default = Total Steps - 3.",
                "22",
                ViewMin: -1,
                ViewMax: 100,
                Min: -1,
                Max: 100,
                Toggleable: true,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 7
            )
        );

        SpectrumStepsParam = T2IParamTypes.Register<int>(
            new(
                "Spectrum - steps",
                "Additional manual passthrough of KSampler step count into the model forecaster (t_max / _taus normalization). Set to match your KSampler total steps for stable forecast accuracy and drift reduction. Default = Total sampling steps.",
                "25",
                ViewMin: 10,
                ViewMax: 100,
                Min: 10,
                Max: 100,
                Toggleable: true,
                Group: SpectrumParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 8
            )
        );

        WorkflowGenerator.AddModelGenStep(
            g =>
            {
                if (g.UserInput.TryGet(SpectrumWParam, out float wcheck))
                {
                    string spectrumNode = g.CreateNode(
                        "SpectrumSDXL",
                        new JObject()
                        {
                            ["model"] = g.LoadingModel,
                            ["w"] = g.UserInput.TryGet(SpectrumWParam, out float w) ? w : 0.3f,
                            ["m"] = g.UserInput.TryGet(SpectrumMParam, out int m) ? m : 3,
                            ["lam"] = g.UserInput.TryGet(SpectrumLamParam, out float lam)
                                ? lam
                                : 0.1f,
                            ["window_size"] = g.UserInput.TryGet(
                                SpectrumWindowSizeParam,
                                out int ws
                            )
                                ? ws
                                : 2,
                            ["flex_window"] = g.UserInput.TryGet(
                                SpectrumFlexWindowParam,
                                out float fw
                            )
                                ? fw
                                : 0.25f,
                            ["warmup_steps"] = g.UserInput.TryGet(
                                SpectrumWarmupStepsParam,
                                out int wms
                            )
                                ? wms
                                : 6,
                            ["stop_caching_step"] =
                                g.UserInput.TryGet(SpectrumStopCachingStepParam, out int scs) ? scs
                                : g.UserInput.TryGet(T2IParamTypes.Steps, out int steps)
                                    ? steps - 3 > 0 ? steps - 3
                                        : 0
                                : 22,
                            ["steps"] =
                                g.UserInput.TryGet(SpectrumStepsParam, out int ss) ? ss
                                : g.UserInput.TryGet(T2IParamTypes.Steps, out int kSteps) ? kSteps
                                : 25,
                        }
                    );
                    g.LoadingModel = [spectrumNode, 0];
                }
            },
            -7
        );
    }
}
