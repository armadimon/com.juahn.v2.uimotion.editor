using System;

namespace Juahn.UiMotion.Editor
{
    /// <summary>카탈로그에 오른 노드 하나.</summary>
    public sealed class MotionNodeEntry
    {
        public Type Type;

        /// <summary>팔레트에 뜨는 이름. 어트리뷰트가 없으면 타입 이름에서 만든다.</summary>
        public string Name;

        public string Category;
        public string Summary;
        public string Sample;

        /// <summary><c>[MotionNode]</c>가 달려 있는가.</summary>
        public bool HasAttribute;

        /// <summary>
        /// <c>[Serializable]</c>이 달려 있는가. 없으면 <c>SerializeReference</c>가 저장하지 못해
        /// 그래프를 다시 열었을 때 <b>노드가 사라진다</b>. 조용히 일어나므로 반드시 잡아야 한다.
        /// </summary>
        public bool IsSerializable;

        /// <summary>설명과 예시를 모두 갖췄는가.</summary>
        public bool HasDocs;

        /// <summary>배포해도 되는가. 팔레트의 "미검증" 섹션 여부를 정한다.</summary>
        public bool IsVerified => HasAttribute && HasDocs && IsSerializable;

        public bool IsFlow;
    }
}
