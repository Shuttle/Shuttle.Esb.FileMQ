using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileDistributorTest : DistributorFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_be_able_to_distribute_messages(bool isTransactionalEndpoint)
		{
			await TestDistributor(FileQueueFixture.GetServiceCollection(), FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", isTransactionalEndpoint, 300);
		}
	}
}