using System.Threading;
using System.Windows.Data;

namespace SyncTrayzor.Xaml
{
    // See http://stackoverflow.com/a/14163432/1086121
    public class CultureAwareBinding : Binding
    {
        public CultureAwareBinding()
        {
            ConverterCulture = Thread.CurrentThread.CurrentCulture;
        }

        public CultureAwareBinding(string path)
            : base(path)
        {
            ConverterCulture = Thread.CurrentThread.CurrentCulture;
        }
    }
}
