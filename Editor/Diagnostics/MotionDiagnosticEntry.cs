namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 모아 둔 진단 하나.
    ///
    /// <b>오브젝트 참조를 들지 않는다.</b> 진단은 그것을 낸 오브젝트보다 오래 살아야
    /// 하는데(그것이 이 목록의 존재 이유다) 참조를 붙잡으면 파괴된 오브젝트를 붙잡고
    /// 있게 된다. 그래서 <see cref="InstanceId"/>와 이름만 들고, 누를 때 그 id로
    /// 오브젝트를 다시 찾는다. 사라졌으면 이름만 보여 준다.
    /// </summary>
    public sealed class MotionDiagnosticEntry
    {
        public MotionIssueLevel Level;

        public string Message;

        /// <summary>문맥 오브젝트의 id. <see cref="MotionObjectId.None"/>이면 문맥이 없다.</summary>
        public long InstanceId;

        /// <summary>오브젝트가 사라진 뒤에도 어디였는지 알 수 있게 떠 둔 이름.</summary>
        public string ContextName;

        /// <summary>같은 이름의 오브젝트가 여럿일 때 구분에 보탬이 되는 타입 이름.</summary>
        public string ContextType;

        /// <summary>같은 진단이 몇 번 났는가. 새 항목을 만들지 않고 이것만 올린다.</summary>
        public int Count;

        /// <summary><c>EditorApplication.timeSinceStartup</c> 기준.</summary>
        public double FirstSeen;

        public double LastSeen;

        /// <summary>어디서 났는지. 문맥이 없으면 빈 문자열이 아니라 사람이 읽을 말을 돌려준다.</summary>
        public string DescribeContext()
        {
            if (InstanceId == MotionObjectId.None)
            {
                return "(문맥 없음)";
            }

            if (string.IsNullOrEmpty(ContextName))
            {
                return "(이름 없음)";
            }

            return string.IsNullOrEmpty(ContextType)
                ? ContextName
                : ContextName + "  (" + ContextType + ")";
        }
    }
}
