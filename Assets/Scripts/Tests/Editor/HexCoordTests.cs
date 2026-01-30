using NUnit.Framework;
using TowerDefense.Grid;

namespace TowerDefense.Tests
{
    public class HexCoordTests
    {
        [Test]
        public void HexCoord_GetNeighbor_ReturnsCorrectNeighbor()
        {
            var center = new HexCoord(0, 0);

            Assert.AreEqual(new HexCoord(1, 0), center.GetNeighbor(0));
            Assert.AreEqual(new HexCoord(0, 1), center.GetNeighbor(1));
            Assert.AreEqual(new HexCoord(-1, 1), center.GetNeighbor(2));
            Assert.AreEqual(new HexCoord(-1, 0), center.GetNeighbor(3));
            Assert.AreEqual(new HexCoord(0, -1), center.GetNeighbor(4));
            Assert.AreEqual(new HexCoord(1, -1), center.GetNeighbor(5));
        }

        [Test]
        public void HexCoord_OppositeEdge_ReturnsCorrectOpposite()
        {
            Assert.AreEqual(3, HexCoord.OppositeEdge(0));
            Assert.AreEqual(4, HexCoord.OppositeEdge(1));
            Assert.AreEqual(5, HexCoord.OppositeEdge(2));
            Assert.AreEqual(0, HexCoord.OppositeEdge(3));
            Assert.AreEqual(1, HexCoord.OppositeEdge(4));
            Assert.AreEqual(2, HexCoord.OppositeEdge(5));
        }

        [Test]
        public void HexCoord_Equality_WorksCorrectly()
        {
            var a = new HexCoord(1, 2);
            var b = new HexCoord(1, 2);
            var c = new HexCoord(2, 1);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void HexCoord_S_CalculatesCorrectly()
        {
            var coord = new HexCoord(1, 2);
            Assert.AreEqual(-3, coord.S);
        }
    }
}