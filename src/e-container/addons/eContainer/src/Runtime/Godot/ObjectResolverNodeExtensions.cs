using Godot;
using VContainer;

namespace Enaweg.Container.Godot;

public static class ObjectResolverNodeExtensions
{
	public static void InjectNode(this IObjectResolver resolver, Node node)
        {
            void InjectNodeRecursive(Node current)
            {
                if (current == null) return;
                
	            resolver.Inject(current);

                var childCount = current.GetChildCount();
                for (var i = 0; i < childCount; i++)
                {
                    var child = current.GetChild(i);
                    InjectNodeRecursive(child);
                }
            }

            InjectNodeRecursive(node);
        }

        public static T Instantiate<T>(this IObjectResolver resolver, PackedScene prefab, Node parent) where T : Node
        {
            var instance = prefab.Instantiate<T>();
            parent.AddChild(instance);
	        resolver.InjectNode(instance);
            return instance;
        }
}
