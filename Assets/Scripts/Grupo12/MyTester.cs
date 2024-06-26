using NavigationDJIA.World;
using QMind.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MyTester : IQMind
{
    private WorldInfo _worldInfo;
    private QTable _QTable;

    public void Initialize(WorldInfo worldInfo)
    {
        _worldInfo = worldInfo;
        _QTable = new QTable(worldInfo); // Se inicializa la QTable con el mundo
        _QTable.initTable();
        LoadQTable();
    }

    public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
    {
        QState state = CalculateState(currentPosition, otherPosition);
        CellInfo agentCell = null;
        int action = GetAction(state, currentPosition, otherPosition);
        agentCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo);
        Debug.Log("Action = " + action);

        // Intentar una nueva acción si la celda no es transitable
        int retries = 0;
        while (!agentCell.Walkable && retries < 4)
        {
            action = GetAction(state, currentPosition, otherPosition);
            agentCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo);
            retries++;
        }

        Debug.Log(currentPosition.x.ToString() + "" + currentPosition.y.ToString());
        return agentCell;
    }

    private int GetAction(QState state, CellInfo currentPosition, CellInfo otherPosition)
    {
        int bestAction = _QTable.GetBestAction(state._nWalkable, state._sWalkable, state._eWalkable, state._wWalkable, state._playerUp, state._playerRight);
        float maxDistance = 0;

        for (int action = 0; action < 4; action++)
        {
            CellInfo nextCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo);
            if (nextCell.Walkable)
            {
                float distance = nextCell.Distance(otherPosition, CellInfo.DistanceType.Manhattan);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    bestAction = action;
                }
            }
        }

        return bestAction;
    }

    private void LoadQTable()
    {
        string filePath = @"Assets/Scripts/Grupo12/TablaQ.csv";
        if (File.Exists(filePath))
        {
            using (StreamReader reader = new StreamReader(File.OpenRead(filePath)))
            {
                int rowCount = 0;
                while (!reader.EndOfStream && rowCount < _QTable._numRows)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');
                    for (int col = 0; col < values.Length; col++)
                    {
                        _QTable._tablaQ[rowCount, col] = float.Parse(values[col]);
                    }
                    rowCount++;
                }
            }
        }
    }

    private QState CalculateState(CellInfo currentPosition, CellInfo otherPosition)
    {
        return new QState(currentPosition, otherPosition, _worldInfo);
    }
}
