using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 슬롯 이름과 같은 이름의 자식을 계층에서 찾아 채운다.
    ///
    /// <b>버튼을 눌렀을 때만 돈다.</b> 런타임 경로 탐색을 쓰지 않는 이유는, 나중에 오브젝트
    /// 이름이 바뀌었을 때 연출이 조용히 사라지는 대신 인스펙터에서 눈에 보이게 하기 위해서다.
    /// 결과가 인스펙터에 그대로 박히므로 diff에도 남는다.
    ///
    /// <b>되돌리기는 호출자의 몫이다.</b> 여기서는 <c>Undo</c>를 부르지 않는다 —
    /// 부르는 쪽이 <see cref="MotionPlayer"/>를 이미 기록해 두어야 한다.
    /// </summary>
    public static class SlotAutoBinder
    {
        public struct Result
        {
            public int Bound;
            public int AlreadyBound;
            public int NotFound;
        }

        /// <summary>
        /// <paramref name="overwrite"/>가 false면 이미 채워진 슬롯은 건드리지 않는다.
        /// 손으로 예외를 잡아 둔 것을 버튼 한 번에 날리면 안 되기 때문이다.
        /// </summary>
        public static Result Bind(MotionPlayer player, bool overwrite)
        {
            var result = new Result();

            if (player == null || player.Graph == null)
            {
                return result;
            }

            IReadOnlyList<SlotDeclaration> slots = player.Graph.Slots;
            var lookup = new Dictionary<string, Transform>();
            Collect(player.transform, lookup);

            for (int i = 0; i < slots.Count; i++)
            {
                SlotDeclaration slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Name))
                {
                    continue;
                }

                if (!overwrite && FindExisting(player, slot.Name) != null)
                {
                    result.AlreadyBound++;
                    continue;
                }

                Transform found;
                if (!lookup.TryGetValue(slot.Name, out found))
                {
                    result.NotFound++;
                    continue;
                }

                Object target = Coerce(found, slot.RequiredType);
                if (target == null)
                {
                    result.NotFound++;
                    continue;
                }

                player.Bind(slot.Name, target);
                result.Bound++;
            }

            return result;
        }

        private static Object FindExisting(MotionPlayer player, string slotName)
        {
            IReadOnlyList<SlotBinding> bindings = player.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Name == slotName && bindings[i].Target != null)
                {
                    return bindings[i].Target;
                }
            }

            return null;
        }

        /// <summary>
        /// 계층을 훑어 이름 -> Transform 표를 만든다.
        ///
        /// <b>같은 이름이 둘 이상이면 먼저 만난 것이 이긴다</b>(너비 우선이라 얕은 쪽이 먼저다).
        /// 사람이 그 이름으로 슬롯을 만들었다면 대개 가까운 쪽을 뜻한다. 애매한 경우는
        /// 인스펙터가 결과를 보여 주므로 사람이 확인할 수 있다.
        /// </summary>
        private static void Collect(Transform root, Dictionary<string, Transform> into)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (!into.ContainsKey(current.name))
                {
                    into[current.name] = current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }
        }

        /// <summary>
        /// 요구 타입으로 바꾼다. 요구 타입이 없으면(어트리뷰트를 안 단 슬롯) Transform 그대로.
        /// </summary>
        private static Object Coerce(Transform found, System.Type requiredType)
        {
            if (requiredType == null)
            {
                return found;
            }

            if (requiredType == typeof(GameObject))
            {
                return found.gameObject;
            }

            if (requiredType.IsInstanceOfType(found))
            {
                return found;
            }

            return found.GetComponent(requiredType);
        }
    }
}
