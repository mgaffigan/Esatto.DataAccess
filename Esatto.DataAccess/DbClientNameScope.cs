using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    public sealed class DbClientNameScope : IDisposable
    {
        private static readonly AsyncLocal<string> _CurrentName = new AsyncLocal<string>();
        public static string CurrentName
        {
            get => _CurrentName.Value;
            set => _CurrentName.Value = value;
        }

        public static string StaticName { get; set; }

        static DbClientNameScope()
        {
            try 
            {
                StaticName = Assembly.GetEntryAssembly()?.GetName().Name
                    ?? AppDomain.CurrentDomain.FriendlyName
                    ?? Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
                StaticName = "Unknown";
            }
        }

        private DbClientNameScope(string name)
        {
            CurrentName = name;
        }

        public void Dispose()
        {
            CurrentName = null;
        }

        public static IDisposable EnterIfNotSet(string name)
        {
            if (CurrentName != null)
            {
                return new NullDisposable();
            }

            return new DbClientNameScope(name);
        }

        public static IDisposable Enter(string name)
        {
            if (CurrentName != null)
            {
                throw new InvalidOperationException($"Already in context '{CurrentName}'");
            }

            return new DbClientNameScope(name);
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose()
            {
                // no-op
            }
        }
    }
}
