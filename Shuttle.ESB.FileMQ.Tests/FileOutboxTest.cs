using NUnit.Framework;
using Shuttle.ESB.Tests;

namespace Shuttle.ESB.FileMQ.Tests
{
	public class FileOutboxTest : OutboxFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_handle_errors(bool isTransactionalEndpoint)
		{
			TestOutboxSending(FileMQExtensions.FileUri(), isTransactionalEndpoint);
		}
	}
}