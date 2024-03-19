using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace AStar.Algolgorithms
{
	public class AStarBoolMap
	{
		// Calculates the Manhattan heuristic distance between two nodes
		private static float calcHeuristicManhattan(int nodeX, int nodeY, int goalX, int goalY)
		{
			return Mathf.Abs(nodeX - goalX) + Mathf.Abs(nodeY - goalY);
		}

		// Calculates the Euclidean heuristic distance between two nodes
		private static float calcHeuristicEuclidean(int nodeX, int nodeY, int goalX, int goalY)
		{
			return Mathf.Sqrt(Mathf.Pow(nodeX - goalX, 2) + Mathf.Pow(nodeY - goalY, 2));
		}

		// Backtraces the path from the goal node to the start node based on the parent map
		private static (int, int)[] backtracePath((int, int)[,] parentMap, int goalX, int goalY)
		{
			List<(int, int)> path = new List<(int, int)>();

			(int, int) current = (goalX, goalY);

			while (current != (-1, -1))
			{
				path.Add(current);
				current = parentMap[current.Item2, current.Item1];
			}

			path.Reverse();
			return path.ToArray();
		}

		// Returns an array of neighbour nodes for a given node
		private static (bool, (int, int))[] getNeighbours(int xCordinate, int yCordinate, bool[,] walkableMap, bool walkableDiagonals = false)
		{

			List<(bool, (int, int))> neighbourCells = new List<(bool, (int, int))>();

			int heigth = walkableMap.GetLength(0);
			int width = walkableMap.GetLength(1);

			int range = 1;
			int yStart = (int)MathF.Max(0, yCordinate - range);
			int yEnd = (int)MathF.Min(heigth - 1, yCordinate + range);

			int xStart = (int)MathF.Max(0, xCordinate - range);
			int xEnd = (int)MathF.Min(width - 1, xCordinate + range);

			for (int y = yStart; y <= yEnd; y++)
			{
				for (int x = xStart; x <= xEnd; x++)
				{
					if (x == xCordinate && y == yCordinate)
					{
						continue;
					}

					if (!walkableMap[y, x])
					{
						continue;
					}

					if (!walkableDiagonals && // If we are not allowing diagonal movement
					(x == xStart || x == xEnd) && (y == yStart || y == yEnd) && // If the node is a diagonal node
					!walkableMap[y, xCordinate] && !walkableMap[yCordinate, x]) // If the node is not reachable from the current node
					{
						continue;
					}

					neighbourCells.Add((walkableMap[y, x], (x, y)));
				}
			}

			return neighbourCells.ToArray();
		}

		//This algorithm is based on the psudo code from https://en.wikipedia.org/wiki/A*_search_algorithm

		// Generates a path between a start node and a goal node
		public static (int, int)[] GeneratePath(int startX, int startY, int goalX, int goalY, bool[,] walkableMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			// Set the heuristic function to use based on the manhattanHeuristic parameter
			Func<int, int, int, int, float> heuristic = manhattanHeuristic ? calcHeuristicManhattan : calcHeuristicEuclidean;

			// Get the dimensions of the map
			int mapHeight = walkableMap.GetLength(0);
			int mapWidth = walkableMap.GetLength(1);

			// Define constants and arrays needed for the algorithm
			float sqrt2 = Mathf.Sqrt(2);
			float[,] gCostMap = new float[mapHeight, mapWidth];
			float[,] fCostMap = new float[mapHeight, mapWidth];
			(int, int)[,] parentMap = new (int, int)[mapHeight, mapWidth];

			// Initialize the open set with the starting node
			SimplePriorityQueue<(int, int)> openSet = new SimplePriorityQueue<(int, int)>();
			gCostMap[startY, startX] = 0.0000001f;
			fCostMap[startY, startX] = heuristic(startX, startY, goalX, goalY);
			parentMap[startY, startX] = (-1, -1);
			openSet.Enqueue((startX, startY), fCostMap[startY, startX]);

			// Start the A* algorithm
			while (openSet.Count > 0)
			{
				// Get the node with the lowest f cost from the open set
				(int, int) current = openSet.First;
				int currentX = current.Item1;
				int currentY = current.Item2;

				// If we have reached the goal node, backtrack to get the path
				if (current.Item1 == goalX && current.Item2 == goalY)
				{
					(int, int)[] path = backtracePath(parentMap, goalX, goalY);
					return path;
				}

				// Remove the current node from the open set
				openSet.Dequeue();

				// Get the neighbours of the current node
				(bool, (int, int))[] neighbours = getNeighbours(currentX, currentY, walkableMap, walkableDiagonals);

				// Process each neighbour
				for (int i = 0; i < neighbours.Length; i++)
				{
					(bool, (int, int)) neighbour = neighbours[i];

					// Get the x and y coordinates of the neighbour
					int neighbourX = neighbour.Item2.Item1;
					int neighbourY = neighbour.Item2.Item2;

					// Calculate the tentative g cost of the neighbour
					float dist = currentX - neighbourX == 0 || currentY - neighbourY == 0 ? 1 : sqrt2;
					float tentativeGCost = gCostMap[currentY, currentX] + dist;

					// If the neighbour has not been visited yet, or the tentative g cost is lower than its current g cost,
					// update its g, f and parent values and add it to the open set
					if (gCostMap[neighbourY, neighbourX] == 0 || tentativeGCost < gCostMap[neighbourY, neighbourX])
					{
						parentMap[neighbourY, neighbourX] = current;
						gCostMap[neighbourY, neighbourX] = tentativeGCost;
						fCostMap[neighbourY, neighbourX] = tentativeGCost + heuristic(neighbourX, neighbourY, goalX, goalY);

						// If the neighbor node is not in the open set, add it
						if (!openSet.Contains(neighbour.Item2))
						{
							openSet.Enqueue(neighbour.Item2, fCostMap[neighbourY, neighbourX]);
						}
					}
				}
			}

			// Returns a empty path, if none could be found
			return new (int, int)[0];
		}

		public static (int, int)[] GeneratePathDebug(int startX, int startY, int goalX, int goalY, bool[,] walkableMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			Stopwatch stopwatchTotal = new Stopwatch();
			Stopwatch stopwatchInit = new Stopwatch();
			Stopwatch stopwatchNode = new Stopwatch();
			Stopwatch stopwatchNeighbours = new Stopwatch();
			Stopwatch stopwatchPrioQueue = new Stopwatch();

			Stopwatch stopwatchPrioQueueFirst = new Stopwatch();
			Stopwatch stopwatchPrioQueueEnqueue = new Stopwatch();
			Stopwatch stopwatchPrioQueueDequeue = new Stopwatch();

			stopwatchTotal.Start();
			stopwatchInit.Start();

			Func<int, int, int, int, float> heuristic = manhattanHeuristic ? calcHeuristicManhattan : calcHeuristicEuclidean;

			int mapHeight = walkableMap.GetLength(0);
			int mapLength = walkableMap.GetLength(1);

			float sqrt2 = Mathf.Sqrt(2);
			float[,] gCostMap = new float[mapHeight, mapLength];
			float[,] fCostMap = new float[mapHeight, mapLength];
			(int, int)[,] parentMap = new (int, int)[mapHeight, mapLength];

			stopwatchPrioQueue.Start();
			SimplePriorityQueue<(int, int)> openSet = new SimplePriorityQueue<(int, int)>();
			stopwatchPrioQueue.Stop();
			gCostMap[startY, startX] = 0.0000001f;
			fCostMap[startY, startX] = heuristic(startX, startY, goalX, goalY);
			parentMap[startY, startX] = (-1, -1);
			stopwatchPrioQueue.Start();
			stopwatchPrioQueueEnqueue.Start();
			openSet.Enqueue((startX, startY), fCostMap[startY, startX]);
			stopwatchPrioQueueEnqueue.Stop();
			stopwatchPrioQueue.Stop();

			stopwatchInit.Stop();

			while (openSet.Count > 0)
			{
				stopwatchNode.Start();

				stopwatchPrioQueue.Start();
				stopwatchPrioQueueFirst.Start();
				(int, int) current = openSet.First;
				stopwatchPrioQueueFirst.Stop();
				stopwatchPrioQueue.Stop();
				int currentX = current.Item1;
				int currentY = current.Item2;

				if (current.Item1 == goalX && current.Item2 == goalY)
				{
					stopwatchNode.Stop();

					(int, int)[] path = backtracePath(parentMap, goalX, goalY);
					stopwatchTotal.Stop();

					UnityEngine.Debug.Log($"Init took: {stopwatchInit.ElapsedMilliseconds}ms, Node took: {stopwatchNode.ElapsedMilliseconds}ms, neighbours took: {stopwatchNeighbours.ElapsedMilliseconds}, prioQueue took: {stopwatchPrioQueue.ElapsedMilliseconds}, total took: {stopwatchTotal.ElapsedMilliseconds}");
					UnityEngine.Debug.Log($"first took: {stopwatchPrioQueueFirst.ElapsedMilliseconds}ms, enqueue took: {stopwatchPrioQueueEnqueue.ElapsedMilliseconds}ms, dequeue took: {stopwatchPrioQueueDequeue.ElapsedMilliseconds}ms");
					return path;
				}
				stopwatchPrioQueue.Start();
				stopwatchPrioQueueDequeue.Start();
				openSet.Dequeue();
				stopwatchPrioQueueDequeue.Stop();
				stopwatchPrioQueue.Stop();
				(bool, (int, int))[] neighbours = getNeighbours(currentX, currentY, walkableMap, walkableDiagonals);

				stopwatchNode.Stop();

				stopwatchNeighbours.Start();

				for (int i = 0; i < neighbours.Length; i++)
				{
					(bool, (int, int)) neighbour = neighbours[i];

					if (!neighbour.Item1)
					{
						continue;
					}

					int neighbourX = neighbour.Item2.Item1;
					int neighbourY = neighbour.Item2.Item2;

					float dist = currentX - neighbourX == 0 || currentY - neighbourY == 0 ? 1 : sqrt2;
					float tentativeGCost = gCostMap[currentY, currentX] + dist;

					if (gCostMap[neighbourY, neighbourX] == 0 || tentativeGCost < gCostMap[neighbourY, neighbourX])
					{
						parentMap[neighbourY, neighbourX] = current;
						gCostMap[neighbourY, neighbourX] = tentativeGCost;
						fCostMap[neighbourY, neighbourX] = tentativeGCost + heuristic(neighbourX, neighbourY, goalX, goalY);

						stopwatchPrioQueueEnqueue.Start();
						if (!openSet.Contains(neighbour.Item2))
						{
							openSet.Enqueue(neighbour.Item2, fCostMap[neighbourY, neighbourX]);
						}
						stopwatchPrioQueueEnqueue.Stop();
					}
				}

				stopwatchNeighbours.Stop();
			}

			stopwatchTotal.Stop();

			UnityEngine.Debug.Log($"Init took: {stopwatchInit.ElapsedMilliseconds}ms, Node took: {stopwatchNode.ElapsedMilliseconds}ms, neighbours took: {stopwatchNeighbours.ElapsedMilliseconds}, prioQueue took: {stopwatchPrioQueue.ElapsedMilliseconds}, total took: {stopwatchTotal.ElapsedMilliseconds}");
			UnityEngine.Debug.Log($"Init took: {stopwatchInit.ElapsedMilliseconds}ms, Node took: {stopwatchNode.ElapsedMilliseconds}ms, neighbours took: {stopwatchNeighbours.ElapsedMilliseconds}, prioQueue took: {stopwatchPrioQueue.ElapsedMilliseconds}, total took: {stopwatchTotal.ElapsedMilliseconds}");

			return new (int, int)[0];
		}
	}
}
