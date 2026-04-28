using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal abstract class BuildPhaseStrategy
    {
        protected readonly GraphContext _context;
        protected readonly GraphSystem _system;
        public string StepName => GetType().Name;

        public abstract void Execute();

        protected BuildPhaseStrategy(GraphContext context, GraphSystem system)
        {
            _context = context;
            _system = system;
        }
    }
}