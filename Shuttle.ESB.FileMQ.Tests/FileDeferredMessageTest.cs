using NUnit.Framework;
using Shuttle.ESB.Tests;

namespace Shuttle.ESB.FileMQ.Tests
{
	public class FileDeferredMessageTest : DeferredFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_perform_full_processing(bool isTransactionalEndpoint)
		{
			TestDeferredProcessing(FileMQExtensions.FileUri(), isTransactionalEndpoint);
		}
	}
}