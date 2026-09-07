using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 오브젝트를 <b>참조 없이</b> 가리키는 수와 그 되찾기.
    ///
    /// 진단은 그것을 낸 오브젝트보다 오래 살아야 한다 — 그것이 진단 목록의 존재 이유다.
    /// 그런데 참조를 붙잡으면 파괴된 오브젝트를 붙잡고 있게 되고, Unity의 파괴된 오브젝트는
    /// <c>null</c>처럼 굴지만 실제로는 살아 있어 목록이 그것을 놓아주지 않는다.
    ///
    /// <b>버전 차이를 여기 한 곳에 가둔다.</b> 6000.3이 <c>GetInstanceID</c>·
    /// <c>InstanceIDToObject</c>를 폐기하고 <c>EntityId</c> 쪽으로 옮겼는데 <c>EntityId</c>는
    /// 6000.0에 없다. 이 패키지는 <c>package.json</c>에 6000.0을 선언하므로 양쪽을 다 살린다.
    /// 조건이 뒤집혀 있는 것은 의도한 것이다 — 컴파일 게이트(dotnet)에는 <c>UNITY_</c> 심볼이
    /// 없고, 그 게이트가 참조하는 DLL은 설치된 최신 에디터의 것이므로 심볼이 없을 때
    /// 최신 쪽으로 가야 한다. <see cref="MotionGraphWindow"/>가 같은 이유로 같은 모양을 쓴다.
    ///
    /// <c>long</c>인 이유는 <c>EntityId</c>가 언젠가 <c>int</c>로 표현되지 않기 때문이다.
    /// </summary>
    public static class MotionObjectId
    {
        /// <summary>오브젝트가 없으면 <see cref="None"/>.</summary>
        public const long None = 0L;

        public static long Of(Object target)
        {
            if (target == null)
            {
                return None;
            }

#if UNITY_6000_0_OR_NEWER && !UNITY_6000_3_OR_NEWER
            return target.GetInstanceID();
#else
            return (long)EntityId.ToULong(target.GetEntityId());
#endif
        }

        /// <summary>파괴됐거나 없는 id면 <c>null</c>.</summary>
        public static Object Find(long id)
        {
            if (id == None)
            {
                return null;
            }

#if UNITY_6000_0_OR_NEWER && !UNITY_6000_3_OR_NEWER
            return EditorUtility.InstanceIDToObject((int)id);
#else
            return EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)id));
#endif
        }
    }
}
