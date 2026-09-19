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
            // Inject before the node enters the tree. AddChild() runs _EnterTree and _Ready
            // synchronously, and those callbacks are the most likely place to use an injected
            // member - injecting afterwards leaves them null exactly when they are needed.
            resolver.InjectNode(instance);
            parent.AddChild(instance);
            return instance;
        }
}
