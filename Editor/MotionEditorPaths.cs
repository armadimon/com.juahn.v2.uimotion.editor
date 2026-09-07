namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 툴 전체가 공유하는 이름과 경로.
    ///
    /// 메뉴 경로를 문자열 리터럴로 흩어 두면 하나를 고칠 때 나머지가 남아
    /// 메뉴가 두 군데로 갈라진다.
    /// </summary>
    public static class MotionEditorPaths
    {
        public const string MenuRoot = "Window/UI Motion/";
        public const string AssetMenuRoot = "Assets/UI Motion/";

        public const string GraphWindowTitle = "UI Motion";
        public const string DoctorWindowTitle = "Node Doctor";
        public const string BrowserWindowTitle = "Motion Presets";
        public const string DiagnosticsWindowTitle = "Motion Diagnostics";

        /// <summary>
        /// 이 패키지가 들고 다니는 예시 그래프의 위치.
        ///
        /// <b><c>Samples~</c>가 아니다.</b> Unity는 <c>~</c>로 끝나는 폴더를 아예 임포트하지
        /// 않으므로 그 안의 에셋은 <c>AssetDatabase</c>로 읽을 수 없다. 그러면 팔레트에서
        /// 노드에 호버했을 때 예시를 그 자리에서 재생한다는 스펙 7.1의 요구를 지킬 수 없다.
        ///
        /// 프로젝트가 자기 노드를 추가하면 그 예시는 프로젝트 어딘가에 있다. 그래서
        /// 예시를 찾을 때는 이 경로가 아니라 <b>이름으로 프로젝트 전체를 검색한다</b>.
        /// 이 상수는 이 패키지가 자기 예시를 어디에 만들지 정할 때만 쓴다.
        /// </summary>
        public const string SamplesFolder = "Editor/Samples/Nodes";

        public const string GraphAssetExtension = "asset";
    }
}
