using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enaweg.Container.Internal
{
    /// <summary>
    /// Copy of internal: https://github.com/hadashiA/VContainer/blob/0093a457d1abe17bbb97d9c0b7dea921c5e670e4/VContainer/Assets/VContainer/Runtime/Internal/CompositeDisposable.cs#L7
    /// </summary>
    sealed class CompositeDisposable : IDisposable
    {
        readonly Stack<IDisposable> disposables = new Stack<IDisposable>();

        public void Dispose()
        {
            IDisposable? disposable;
            do
            {
                lock (disposables)
                {
                    disposable = disposables.Count > 0
                        ? disposables.Pop()
                        : null;
                }
                disposable?.Dispose();
            } while (disposable != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(IDisposable disposable)
        {
            lock (disposables)
            {
                disposables.Push(disposable);
            }
        }
    }
}
