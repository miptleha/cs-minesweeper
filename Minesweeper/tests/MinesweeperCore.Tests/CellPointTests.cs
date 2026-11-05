using MinesweeperCore.Domain;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты класса CellPoint — координаты и соседи.
/// </summary>
public class CellPointTests
{
    [Fact]
    public void Neighbors_Returns8_ForCenterCellPoint()
    {
        // Arrange: центральная точка — у неё 8 соседей.
        var center = new CellPoint(4, 4);

        // Act: получаем всех соседей.
        var neighbors = center.Neighbors().ToList();

        // Assert: ровно 8 соседей.
        Assert.Equal(8, neighbors.Count);
    }

    [Fact]
    public void Neighbors_DoesNotIncludeSelf()
    {
        // Arrange: любая точка.
        var point = new CellPoint(5, 5);

        // Act: получаем соседей.
        var neighbors = point.Neighbors();

        // Assert: точка не содержит саму себя.
        Assert.DoesNotContain(point, neighbors);
    }

    [Fact]
    public void Neighbors_ReturnsCorrectCoordinates()
    {
        // Arrange: точка (1,1) — все 8 соседей.
        var point = new CellPoint(1, 1);
        var neighbors = point.Neighbors().OrderBy(n => n.X).ThenBy(n => n.Y).ToList();

        // Act & Assert: проверяем координаты всех 8 соседей по порядку.
        Assert.Equal(8, neighbors.Count);
        Assert.Equal(new CellPoint(0, 0), neighbors[0]);
        Assert.Equal(new CellPoint(0, 1), neighbors[1]);
        Assert.Equal(new CellPoint(0, 2), neighbors[2]);
        Assert.Equal(new CellPoint(1, 0), neighbors[3]);
        Assert.Equal(new CellPoint(1, 2), neighbors[4]);
        Assert.Equal(new CellPoint(2, 0), neighbors[5]);
        Assert.Equal(new CellPoint(2, 1), neighbors[6]);
        Assert.Equal(new CellPoint(2, 2), neighbors[7]);
    }

    [Fact]
    public void GetValidNeighbors_Returns3_ForCornerOn9x9Board()
    {
        // Arrange: поле 9×9, угол (0,0) — только 3 соседа в пределах поля.
        var board = new Board(9, 9);

        // Act: получаем валидных соседей.
        var neighbors = board.GetValidNeighbors(new CellPoint(0, 0)).ToList();

        // Assert: 3 соседа — (1,0), (0,1), (1,1).
        Assert.Equal(3, neighbors.Count);
        Assert.Contains(new CellPoint(1, 0), neighbors);
        Assert.Contains(new CellPoint(0, 1), neighbors);
        Assert.Contains(new CellPoint(1, 1), neighbors);
    }

    [Fact]
    public void GetValidNeighbors_Returns5_ForEdgeOn9x9Board()
    {
        // Arrange: поле 9×9, крайняя точка (0,4) — 5 соседей.
        var board = new Board(9, 9);

        // Act: получаем валидных соседей.
        var neighbors = board.GetValidNeighbors(new CellPoint(0, 4)).ToList();

        // Assert: ровно 5 соседей (крайняя клетка без диагоналей за пределами).
        Assert.Equal(5, neighbors.Count);
    }

    [Fact]
    public void GetValidNeighbors_Returns8_ForCenterOn9x9Board()
    {
        // Arrange: поле 9×9, центр (4,4) — все 8 соседей в пределах.
        var board = new Board(9, 9);

        // Act: получаем валидных соседей.
        var neighbors = board.GetValidNeighbors(new CellPoint(4, 4)).ToList();

        // Assert: ровно 8 соседей.
        Assert.Equal(8, neighbors.Count);
    }
}
