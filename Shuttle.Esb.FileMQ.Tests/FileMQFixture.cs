using Castle.Windsor;
using Shuttle.Core.Castle;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
    public static class FileMQFixture
    {
        public static ComponentContainer GetComponentContainer()
        {
            var container = new WindsorComponentContainer(new WindsorContainer());

            return new ComponentContainer(container, () => container);
        }
    }
}