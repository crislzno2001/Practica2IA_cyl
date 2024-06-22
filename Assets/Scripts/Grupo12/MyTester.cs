using NavigationDJIA.World;
using QMind.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MyTester : IQMind
{
    private WorldInfo _worldInfo; // Informaci�n sobre el mundo en el que se mueve el agente
    private QTable _qTable; // La tabla Q que contiene los valores Q entrenados

    // Inicializa el tester con la informaci�n del mundo y carga la tabla Q desde un archivo
    public void Initialize(WorldInfo worldInfo)
    {
        _worldInfo = worldInfo; // Asigna la informaci�n del mundo
        _qTable = new QTable(worldInfo); // Inicializa la QTable con el mundo
        _qTable.InitializeTable(); // Inicializa la tabla Q con valores predeterminados
        LoadQTable(); // Carga los valores Q desde un archivo
    }

    // Calcula el siguiente paso del agente basado en la posici�n actual y la del otro jugador
    public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
    {
        QState state = CalculateState(currentPosition, otherPosition); // Calcula el estado actual
        int action = GetAction(state); // Obtiene la mejor acci�n basada en la tabla Q
        CellInfo agentCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo); // Mueve el agente seg�n la acci�n seleccionada
        Debug.Log("Action = " + action); // Imprime la acci�n para depuraci�n

        // Si la celda siguiente no es transitable, recalcula el estado (esto puede necesitar una mejor estrategia de manejo)
        if (!agentCell.Walkable)
        {
            state = CalculateState(currentPosition, otherPosition); // Recalcula el estado
        }

        Debug.Log(currentPosition.x.ToString() + "" + currentPosition.y.ToString()); // Imprime la posici�n actual para depuraci�n

        return agentCell; // Devuelve la celda siguiente del agente
    }

    // Obtiene la mejor acci�n para un estado dado usando la tabla Q
    private int GetAction(QState state)
    {
        return _qTable.GetBestAction(state._nWalkable, state._sWalkable, state._eWalkable, state._wWalkable, state._playerUp, state._playerRight);
    }

    // Carga los valores Q desde un archivo CSV en la tabla Q
    private void LoadQTable()
    {
        string filePath = @"Assets/Scripts/Grupo12/TablaQ.csv"; // Ruta del archivo CSV que contiene la tabla Q
        if (File.Exists(filePath)) // Verifica si el archivo existe
        {
            using (StreamReader reader = new StreamReader(File.OpenRead(filePath))) // Abre el archivo para lectura
            {
                int rowCount = 0; // Contador de filas
                while (!reader.EndOfStream && rowCount < _qTable.ActionsCount) // Lee hasta el final del archivo o hasta llenar la tabla Q
                {
                    var line = reader.ReadLine(); // Lee una l�nea del archivo
                    var values = line.Split(';'); // Separa los valores por el delimitador ';'
                    for (int col = 0; col < values.Length; col++) // Itera sobre las columnas
                    {
                        _qTable.QTableValues[rowCount, col] = float.Parse(values[col]); // Asigna el valor Q le�do a la tabla Q
                    }
                    rowCount++; // Incrementa el contador de filas
                }
            }
        }
    }

    // Calcula el estado basado en la posici�n actual del agente y la del otro jugador
    private QState CalculateState(CellInfo currentPosition, CellInfo otherPosition)
    {
        return new QState(currentPosition, otherPosition, _worldInfo); // Crea un nuevo estado QState basado en las posiciones actuales
    }
}
