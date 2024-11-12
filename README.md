GPU를 이용하여 공간 보간(Spatial Interpolation)을 수행하고 그 결과를 표시하는 WPF 기반 샘플 프로그램입니다.  
현재 구현된 공간 보간 기법은 IDW(Inverse Distance Weighting)이며, 차후에는 더 복잡한 Kriging 기법도 구현해보려고 합니다.  

다음은 샘플 프로그램을 CPU 및 GPU 기반으로 실행한 영상입니다.  

https://github.com/user-attachments/assets/d4bf0417-8fd8-42fb-aa4d-3553716c77b1


영상을 보면, 좌측의 샘플 목록 중 선택된 샘플에 대하여 마우스 포인터로 위치를 이동할 수 있게 했습니다. CPU로 계산할 경우 다른 위치로 이동할 때마다 Progress bar가 나타나고, 얼마 후에 결과를 표시합니다. 우측 하단의 ‘Duration’을 보면 약 3초 넘게 걸리는 것을 알 수 있습니다.  

그런데 ‘Use GPU’를 체크한 후 위치를 이동해보면 정말 순식간에 계산됩니다. 심지어 마우스로 자유롭게 드래그를 해도 될 정도로 빠릅니다.  

샘플은 ILGPU, ComputeSharp, SharpDX로 세 가지 라이브러리를 이용하여 구현했습니다.  
만들어보게 된 계기나 자세한 구현 과정은 [블로그 글](https://blog.naver.com/vagabond-k/223658042578)에 공유했습니다.  
.NET 프로젝트에서 GPU를 사용할 필요가 있는 분들께 도움이 되었으면 좋겠습니다.  
