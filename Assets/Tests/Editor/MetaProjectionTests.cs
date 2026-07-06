using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaProjectionTests
    {
        [Test]
        public void ProjectsLoopCountIntoNamedVariable()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(3), state, "회차");
            Assert.AreEqual(VnValue.Int(3), state.Get("회차"));
        }

        [Test]
        public void ProjectionOverwritesOnRepeat()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(1), state, "loop");
            MetaProjection.Project(new MetaState(2), state, "loop");
            Assert.AreEqual(VnValue.Int(2), state.Get("loop"));
        }

        [Test]
        public void RejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            Assert.Throws<System.ArgumentException>(() => MetaProjection.Project(new MetaState(1), state, ""));
        }
    }
}
