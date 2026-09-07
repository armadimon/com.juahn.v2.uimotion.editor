using System;
using System.Collections.Generic;
using UnityEditor;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 프로젝트의 모든 노드 타입을 모은다. 도메인 리로드마다 다시 훑는다.
    ///
    /// <c>TypeCache</c>를 쓰는 이유는 Unity가 이미 만들어 둔 색인이라
    /// 어셈블리를 직접 훑는 것보다 훨씬 빠르기 때문이다.
    /// </summary>
    [InitializeOnLoad]
    public static class MotionNodeCatalog
    {
        private static readonly List<MotionNodeEntry> Entries = new List<MotionNodeEntry>();
        private static readonly List<string> CategoryNames = new List<string>();
        private static readonly Dictionary<Type, MotionNodeEntry> ByType = new Dictionary<Type, MotionNodeEntry>();

        public const string UncategorizedName = "Uncategorized";

        static MotionNodeCatalog()
        {
            Refresh();
        }

        public static IReadOnlyList<MotionNodeEntry> All => Entries;

        /// <summary>카테고리 이름. 알파벳 순이고 <see cref="UncategorizedName"/>이 항상 마지막이다.</summary>
        public static IReadOnlyList<string> Categories => CategoryNames;

        public static MotionNodeEntry Find(Type nodeType)
        {
            if (nodeType == null)
            {
                return null;
            }

            MotionNodeEntry entry;
            return ByType.TryGetValue(nodeType, out entry) ? entry : null;
        }

        /// <summary>새 인스턴스를 만든다. 만들 수 없으면 null.</summary>
        public static MotionNodeBase Create(MotionNodeEntry entry)
        {
            if (entry == null || entry.Type == null)
            {
                return null;
            }

            return Activator.CreateInstance(entry.Type) as MotionNodeBase;
        }

        public static void Refresh()
        {
            Entries.Clear();
            ByType.Clear();
            CategoryNames.Clear();

            var categories = new HashSet<string>(StringComparer.Ordinal);
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<MotionNodeBase>();

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];

                // 추상 베이스(MotionEffectNode, UnityEffectNode, SubGraphNode 등)는 노드가 아니다.
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                // SerializeReference도 그래프 창도 매개변수 없는 생성자를 요구한다.
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                MotionNodeEntry entry = Describe(type);
                Entries.Add(entry);
                ByType[type] = entry;
                categories.Add(entry.Category);
            }

            Entries.Sort(CompareEntries);

            foreach (string category in categories)
            {
                CategoryNames.Add(category);
            }

            CategoryNames.Sort(CompareCategories);
        }

        private static MotionNodeEntry Describe(Type type)
        {
            var attribute = (MotionNodeAttribute)Attribute.GetCustomAttribute(type, typeof(MotionNodeAttribute));

            var entry = new MotionNodeEntry
            {
                Type = type,
                HasAttribute = attribute != null,
                IsSerializable = Attribute.IsDefined(type, typeof(SerializableAttribute)),
                IsFlow = typeof(MotionFlowNode).IsAssignableFrom(type),
            };

            if (attribute == null)
            {
                entry.Name = Humanize(type.Name);
                entry.Category = UncategorizedName;
                entry.HasDocs = false;
                return entry;
            }

            entry.Name = string.IsNullOrWhiteSpace(attribute.Name) ? Humanize(type.Name) : attribute.Name;
            entry.Category = string.IsNullOrWhiteSpace(attribute.Category) ? UncategorizedName : attribute.Category;
            entry.Summary = attribute.Summary;
            entry.Sample = attribute.Sample;
            entry.HasDocs = attribute.IsVerified;
            return entry;
        }

        /// <summary>"PunchScaleNode" -> "Punch Scale". 어트리뷰트가 없는 노드의 표시용이다.</summary>
        private static string Humanize(string typeName)
        {
            if (typeName.EndsWith("Node", StringComparison.Ordinal) && typeName.Length > 4)
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }

            var builder = new System.Text.StringBuilder(typeName.Length + 4);
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(typeName[i]);
            }

            return builder.ToString();
        }

        private static int CompareEntries(MotionNodeEntry a, MotionNodeEntry b)
        {
            int byCategory = CompareCategories(a.Category, b.Category);
            return byCategory != 0 ? byCategory : string.CompareOrdinal(a.Name, b.Name);
        }

        private static int CompareCategories(string a, string b)
        {
            // 분류되지 않은 것은 언제나 맨 뒤로. 대개 아직 정리되지 않은 실험용이다.
            bool aLast = a == UncategorizedName;
            bool bLast = b == UncategorizedName;

            if (aLast != bLast)
            {
                return aLast ? 1 : -1;
            }

            return string.CompareOrdinal(a, b);
        }
    }
}
