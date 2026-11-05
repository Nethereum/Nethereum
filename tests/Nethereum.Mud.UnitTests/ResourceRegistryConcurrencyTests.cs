using System.Reflection;

namespace Nethereum.Mud.UnitTests
{
    public class ResourceRegistryConcurrencyTests
    {
        private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(120);

        public class SlowStartResource<TTag> : IResource
        {
            public SlowStartResource()
            {
                Thread.Sleep(2);
            }

            public string Name => "Slow";
            public string Namespace => "concurrency";
            public byte[] ResourceIdEncoded => new byte[32];
            public byte[] ResourceTypeId => new byte[2];
        }

        public class QuickResource<TTag> : IResource
        {
            public string Name => "Quick";
            public string Namespace => "concurrency";
            public byte[] ResourceIdEncoded => new byte[32];
            public byte[] ResourceTypeId => new byte[2];
        }

        private static readonly Type[] TagTypes = new[] { typeof(object).Assembly, typeof(Uri).Assembly, typeof(Enumerable).Assembly, typeof(List<>).Assembly }
            .Distinct()
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsGenericTypeDefinition && !t.ContainsGenericParameters && !t.IsByRefLike && !t.IsPointer && !t.IsByRef && t != typeof(void))
            .Distinct()
            .OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToArray();

        private static Type[] ResourceTypes(Type genericDefinition, int skip, int count)
        {
            var tags = TagTypes.Skip(skip).Take(count).ToArray();
            Assert.Equal(count, tags.Length);
            return tags.Select(tag => genericDefinition.MakeGenericType(tag)).ToArray();
        }

        private static Func<Type, IResource> GenericAccessor()
        {
            var method = typeof(ResourceRegistry).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(ResourceRegistry.GetResource) && m.IsGenericMethodDefinition);
            return type => (IResource)method.MakeGenericMethod(type).Invoke(null, null)!;
        }

        private static void RunTogether(int threadCount, Action<int> body)
        {
            using var barrier = new Barrier(threadCount);
            var errors = new List<Exception>();
            var threads = Enumerable.Range(0, threadCount).Select(index => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    body(index);
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(ex);
                }
            })
            { IsBackground = true }).ToArray();

            foreach (var thread in threads) thread.Start();

            var deadline = DateTime.UtcNow + HangGuard;
            foreach (var thread in threads)
            {
                var remaining = deadline - DateTime.UtcNow;
                Assert.True(remaining > TimeSpan.Zero && thread.Join(remaining), "a registry call did not return");
            }

            Assert.Empty(errors);
        }

        [Fact]
        public void Given_Concurrent_First_Requests_For_The_Same_Type_When_Resolving_By_Type_Then_Every_Caller_Gets_The_Same_Instance()
        {
            const int rounds = 100;
            const int callers = 8;
            var types = ResourceTypes(typeof(SlowStartResource<>), 0, rounds);

            foreach (var type in types)
            {
                var results = new IResource[callers];
                RunTogether(callers, index => results[index] = ResourceRegistry.GetResource(type));

                Assert.All(results, r => Assert.Same(results[0], r));
                Assert.Same(results[0], ResourceRegistry.ResourceIds[type]);
            }
        }

        [Fact]
        public void Given_Concurrent_First_Requests_For_The_Same_Type_When_Resolving_Generically_Then_Every_Caller_Gets_The_Same_Instance()
        {
            const int rounds = 100;
            const int callers = 8;
            var accessor = GenericAccessor();
            var types = ResourceTypes(typeof(SlowStartResource<>), rounds, rounds);

            foreach (var type in types)
            {
                var results = new IResource[callers];
                RunTogether(callers, index => results[index] = accessor(type));

                Assert.All(results, r => Assert.Same(results[0], r));
                Assert.Same(results[0], ResourceRegistry.ResourceIds[type]);
            }
        }

        [Fact]
        public void Given_Concurrent_First_Registration_Of_Many_Types_When_Threads_Race_Then_No_Entry_Is_Lost_Or_Corrupted()
        {
            const int rounds = 3;
            const int typesPerRound = 360;
            const int threads = 16;

            for (var round = 0; round < rounds; round++)
            {
                var types = ResourceTypes(typeof(QuickResource<>), round * typesPerRound, typesPerRound);
                var seen = new IResource[threads][];

                RunTogether(threads, index =>
                {
                    var mine = new IResource[types.Length];
                    for (var i = 0; i < types.Length; i++)
                    {
                        var slot = (i + index * 17) % types.Length;
                        mine[slot] = ResourceRegistry.GetResource(types[slot]);
                    }
                    seen[index] = mine;
                });

                for (var i = 0; i < types.Length; i++)
                {
                    Assert.True(ResourceRegistry.ResourceIds.TryGetValue(types[i], out var stored));
                    for (var t = 0; t < threads; t++)
                    {
                        Assert.Same(stored, seen[t][i]);
                    }
                }
            }
        }
    }
}
