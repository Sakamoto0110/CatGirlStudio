# Bundled Real-ESRGAN components

The complete Windows package includes the Real-ESRGAN Vulkan executable and
the AnimeVideo-v3 2×, 3×, and 4× model files from the
[Real-ESRGAN v0.2.5.0 Windows archive](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0).

| Component | Source | Included notice |
| --- | --- | --- |
| Real-ESRGAN and AnimeVideo-v3 | [Real-ESRGAN v0.2.5.0](https://github.com/xinntao/Real-ESRGAN/tree/685d429c81888252bdb10f56c7754baededc3823) | [BSD 3-Clause](Real-ESRGAN-LICENSE.txt) |
| Real-ESRGAN ncnn Vulkan and its RealSR base | [Vulkan backend v0.2.0](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/tree/37026f49824c5cf84062e7c6a5dd71445dcf610f) | [MIT notices](Real-ESRGAN-ncnn-vulkan-LICENSE.txt) |
| ncnn | [Tencent ncnn](https://github.com/Tencent/ncnn/tree/6125c9f47cd14b589de0521350668cf9d3d37e3c) | [BSD and third-party notices](ncnn-LICENSE.txt) |
| glslang | [Khronos glslang](https://github.com/KhronosGroup/glslang/tree/4afd69177258d0636f78d2c4efb823ab6382a187) | [License notices](glslang-LICENSE.txt) |
| libwebp | [WebM Project libwebp](https://github.com/webmproject/libwebp/tree/8ea81561d2fdd382da60f57958741a7c23a18eb6) | [License](libwebp-COPYING.txt), [patent grant](libwebp-PATENTS.txt) |
| stb_image and stb_image_write | [Vendored stb headers](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/tree/37026f49824c5cf84062e7c6a5dd71445dcf610f/src) | [MIT](stb-LICENSE.txt) |
| Windows dirent | [Vendored dirent header](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/blob/37026f49824c5cf84062e7c6a5dd71445dcf610f/src/win32dirent.h), copyright 1998–2019 Toni Ronkko | [MIT](dirent-LICENSE.txt) |

`vcomp140.dll` is the Microsoft Visual C++ OpenMP runtime from the upstream
archive. It is covered by Microsoft's [Visual Studio redistribution terms](https://learn.microsoft.com/visualstudio/releases/2022/redistribution).
The Vulkan loader and GPU driver are provided by the system.
