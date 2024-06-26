using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using QMind;
using QMind.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class MyTrainer : IQMindTrainer
{
    public int CurrentEpisode { get; private set; } // Episodio actual del entrenamiento
    public int CurrentStep { get; private set; } // Paso actual dentro del episodio
    public CellInfo AgentPosition { get; private set; } // Posición del agente en el mundo
    public CellInfo OtherPosition { get; private set; } // Posición del otro jugador en el mundo
    public float Return { get; private set; } // Retorno acumulado en el episodio actual
    public float ReturnAveraged { get; private set; } // Retorno promedio a lo largo de los episodios
    public event EventHandler OnEpisodeStarted; // Evento que se dispara cuando comienza un nuevo episodio
    public event EventHandler OnEpisodeFinished; // Evento que se dispara cuando termina un episodio

    private INavigationAlgorithm _navigationAlgorithm; // Algoritmo de navegación utilizado
    private WorldInfo _worldInfo; // Información sobre el mundo donde se mueve el agente
    private QTable _QTable; // Tabla Q que almacena los valores Q para cada estado y acción
    private QMindTrainerParams _params; // Parámetros para el entrenador Q-learning

    private int counter = 0; // Contador de pasos dentro del episodio
    private int numEpisode = 0; // Número total de episodios completados
    private QState currentState; // Estado actual del agente
    private List<float> episodeReturns; // Lista para almacenar retornos de cada episodio

    private List<CellInfo> recentPositions; // Lista para almacenar las posiciones recientes del agente
    private float epsilonDecay = 0.99f; // Parámetro para controlar la disminución de epsilon

    // Constructor
    public MyTrainer()
    {
        episodeReturns = new List<float>(); // Inicializa la lista de retornos por episodio
        recentPositions = new List<CellInfo>(); // Inicializa la lista de posiciones recientes
    }

    // Realiza un paso de entrenamiento o ejecución
    public void DoStep(bool train)
    {
        CellInfo agentNextCell;
        CellInfo currentCell = AgentPosition;
        int action = -1;

        currentState = new QState(AgentPosition, OtherPosition, _worldInfo);
        Debug.Log($"Current State ID: {currentState._idState}");

        int retries = 0;
        do
        {
            if (OnlyWayPossible(out action)) // Si solo hay una dirección posible, se elige esa acción
            {
                agentNextCell = QMind.Utils.MoveAgent(action, AgentPosition, _worldInfo);
            }
            else if (chooseRandom() || isStuck())
            {
                if (isStuck())
                {
                    _params.epsilon *= epsilonDecay; // Disminuye epsilon si está atascado
                }
                action = randomChoice(); // Escoge una acción aleatoria
                agentNextCell = QMind.Utils.MoveAgent(action, AgentPosition, _worldInfo);
            }
            else
            {
                action = bestActionPossible(); // Escoge la mejor acción basada en la tabla Q
                agentNextCell = QMind.Utils.MoveAgent(action, AgentPosition, _worldInfo);
            }

            if (!agentNextCell.Walkable)
            {
                // Penalización por moverse a una celda no transitable
                float Q = ReturnQTable(action);
                float QNew = UpdateQTable(Q, 0, -1000);
                _QTable.UpdateQTable(action, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight, QNew);
            }

            retries++;
        } while (!agentNextCell.Walkable && retries < 4); // Repite hasta que el agente se mueva a una celda transitable

        // Si después de varios intentos no se puede mover, toma una acción aleatoria
        if (!agentNextCell.Walkable)
        {
            action = randomChoice();
            agentNextCell = QMind.Utils.MoveAgent(action, AgentPosition, _worldInfo);
        }

        // Actualiza el valor Q para la acción tomada
        float currentQ = ReturnQTable(action);
        float bestNextQ = GetMaxQ(agentNextCell);
        int reward = GetReward(agentNextCell, action);
        float actualizedQ = UpdateQTable(currentQ, bestNextQ, reward);
        _QTable.UpdateQTable(action, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight, actualizedQ);

        AgentPosition = agentNextCell; // Actualiza la posición del agente
        CellInfo otherCell = QMind.Utils.MoveOther(_navigationAlgorithm, OtherPosition, AgentPosition);
        OtherPosition = otherCell; // Actualiza la posición del otro jugador

        // Actualiza el retorno acumulado
        Return += reward;

        // Guarda la posición actual en la lista de posiciones recientes
        SaveRecentPosition(AgentPosition);

        CurrentStep = counter;
        if (OtherPosition == null || CurrentStep == _params.maxSteps || OtherPosition == AgentPosition)
        {
            // Si se cumple alguna condición de fin de episodio, empieza un nuevo episodio
            OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
            NewEpisode();
        }
        else
        {
            counter += 1;
        }
    }

    // Verifica si solo hay una dirección posible y retorna la acción correspondiente
    private bool OnlyWayPossible(out int action)
    {
        action = -1;
        List<int> posibleActions = new List<int>();

        CellInfo north = QMind.Utils.MoveAgent(0, AgentPosition, _worldInfo);
        if (north.Walkable && !(north.x == OtherPosition.x && north.y == OtherPosition.y))
            posibleActions.Add(0);

        CellInfo east = QMind.Utils.MoveAgent(1, AgentPosition, _worldInfo);
        if (east.Walkable && !(east.x == OtherPosition.x && east.y == OtherPosition.y))
            posibleActions.Add(1);

        CellInfo south = QMind.Utils.MoveAgent(2, AgentPosition, _worldInfo);
        if (south.Walkable && !(south.x == OtherPosition.x && south.y == OtherPosition.y))
            posibleActions.Add(2);

        CellInfo west = QMind.Utils.MoveAgent(3, AgentPosition, _worldInfo);
        if (west.Walkable && !(west.x == OtherPosition.x && west.y == OtherPosition.y))
            posibleActions.Add(3);

        if (posibleActions.Count == 1)
        {
            action = posibleActions[0];
            return true;
        }

        return false;
    }

    // Inicia un nuevo episodio
    private void NewEpisode()
    {
        AgentPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al agente
        OtherPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al otro jugador
        counter = 0;
        CurrentStep = counter;
        numEpisode++;
        CurrentEpisode = numEpisode;

        // Limpiar la lista de posiciones recientes
        recentPositions.Clear();

        // Almacena el retorno del episodio finalizado y calcula el promedio
        if (numEpisode > 1)
        {
            episodeReturns.Add(Return);
            ReturnAveraged = episodeReturns.Average();
        }
        Return = 0; // Reinicia el retorno acumulado para el nuevo episodio

        if (numEpisode % _params.episodesBetweenSaves == 0)
        {
            SaveQTable(); // Guarda la tabla Q cada ciertos episodios
        }
        OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
    }

    // Inicializa el entrenador con los parámetros y el mundo dados
    public void Initialize(QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
    {
        _navigationAlgorithm = Utils.InitializeNavigationAlgo(navigationAlgorithm, worldInfo); // Inicializa el algoritmo de navegación
        _worldInfo = worldInfo; // Asigna la información del mundo
        AgentPosition = worldInfo.RandomCell(); // Asigna una posición aleatoria al agente
        OtherPosition = worldInfo.RandomCell(); // Asigna una posición aleatoria al otro jugador
        OnEpisodeStarted?.Invoke(this, EventArgs.Empty); // Dispara el evento de inicio de episodio

        _params = qMindTrainerParams; // Asigna los parámetros del entrenador
        _params.maxSteps = 5000; // Cambia este valor al número máximo de pasos deseado

        _QTable = new QTable(worldInfo); // Crea una nueva tabla Q
        _QTable.initTable(); // Inicializa la tabla Q
        LoadQTable(); // Cargar la tabla Q desde el archivo
    }

    // Cargar la tabla Q desde el archivo
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

    // Decide si escoger una acción aleatoria
    private bool chooseRandom()
    {
        float azar = UnityEngine.Random.Range(0.0f, 1.0f); // Genera un número aleatorio entre 0 y 1
        return azar <= _params.epsilon; // Decide si tomar una acción aleatoria basado en epsilon
    }

    // Escoge una acción aleatoria
    private int randomChoice()
    {
        return UnityEngine.Random.Range(0, 4); // Retorna un número aleatorio entre 0 y 3, representando las cuatro direcciones posibles
    }

    // Escoge la mejor acción basada en la tabla Q
    private int bestActionPossible()
    {
        return _QTable.GetBestAction(currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight); // Devuelve la mejor acción según la tabla Q
    }

    // Verifica si el agente está atascado
    private bool isStuck()
    {
        if (recentPositions.Count < 3) return false; // Si hay menos de 3 posiciones recientes, no está atascado

        var lastThreePositions = recentPositions.Skip(Math.Max(0, recentPositions.Count() - 3)); // Toma las últimas 3 posiciones
        return lastThreePositions.Distinct().Count() <= 1; // Si las últimas 3 posiciones son iguales, el agente está atascado
    }

    // Guarda la posición actual en la lista de posiciones recientes
    private void SaveRecentPosition(CellInfo position)
    {
        if (recentPositions.Count >= 10) // Si hay más de 10 posiciones recientes, elimina la más antigua
        {
            recentPositions.RemoveAt(0);
        }
        recentPositions.Add(position); // Añade la posición actual a la lista
    }

    // Devuelve el valor Q para la acción actual
    private float ReturnQTable(int accion)
    {
        return _QTable.GetQ(accion, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight); // Devuelve el valor Q actual
    }

    // Devuelve el mejor valor Q para el siguiente estado
    private float GetMaxQ(CellInfo nextCell)
    {
        QState nextState = new QState(nextCell, OtherPosition, _worldInfo); // Crea el siguiente estado
        return _QTable.GetMaxQ(nextState._nWalkable, nextState._sWalkable, nextState._eWalkable, nextState._wWalkable, nextState._playerUp, nextState._playerRight); // Devuelve el mejor valor Q del siguiente estado
    }

    // Verifica si hay una pared entre el agente y el otro jugador
    private bool IsWallBeetween(CellInfo a, CellInfo b)
    {
        if (a == null || b == null) return false;

        // Verificar si hay una pared en la misma fila o columna
        if (a.x == b.x)
        {
            int minY = Math.Min(a.y, b.y);
            int maxY = Math.Max(a.y, b.y);
            for (int y = minY + 1; y < maxY; y++)
            {
                if (_worldInfo[a.x, y].Type == CellInfo.CellType.Wall)
                {
                    return true;
                }
            }
        }
        else if (a.y == b.y)
        {
            int minX = Math.Min(a.x, b.x);
            int maxX = Math.Max(a.x, b.x);
            for (int x = minX + 1; x < maxX; x++)
            {
                if (_worldInfo[x, a.y].Type == CellInfo.CellType.Wall)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Verifica si la celda está en una esquina (dos celdas adyacentes no transitables)
    private bool IsInCorner(CellInfo cell)
    {
        bool left = cell.x > 0 && !_worldInfo[cell.x - 1, cell.y].Walkable;
        bool right = cell.x < _worldInfo.WorldSize.x - 1 && !_worldInfo[cell.x + 1, cell.y].Walkable;
        bool up = cell.y < _worldInfo.WorldSize.y - 1 && !_worldInfo[cell.x, cell.y + 1].Walkable;
        bool down = cell.y > 0 && !_worldInfo[cell.x, cell.y - 1].Walkable;

        // Verificar límites
        bool leftLimit = cell.x == 0;
        bool rightLimit = cell.x == _worldInfo.WorldSize.x - 1;
        bool upLimit = cell.y == _worldInfo.WorldSize.y - 1;
        bool downLimit = cell.y == 0;

        return (left || leftLimit) && (down || downLimit) ||
               (left || leftLimit) && (up || upLimit) ||
               (right || rightLimit) && (down || downLimit) ||
               (right || rightLimit) && (up || upLimit);
    }

    // Calcula la recompensa basada en la nueva posición del agente y la acción tomada
    private int GetReward(CellInfo nextCell, int accion)
    {
        int InitialDistance = Mathf.Abs(AgentPosition.x - OtherPosition.x) + Mathf.Abs(AgentPosition.y - OtherPosition.y); // Calcula la distancia inicial entre el agente y el otro jugador
        int FinalDistance = Mathf.Abs(nextCell.x - OtherPosition.x) + Mathf.Abs(nextCell.y - OtherPosition.y); // Calcula la distancia final entre el agente y el otro jugador

        int reward = 0; // Inicializa la recompensa
        if (nextCell.x == OtherPosition.x && nextCell.y == OtherPosition.y) { return -100; } // Penaliza si el agente se encuentra con el otro jugador
        if (FinalDistance > InitialDistance)
        {
            reward += 100 * (FinalDistance - InitialDistance); // Recompensa proporcional al aumento de la distancia
        }
        else
        {
            if (FinalDistance <= 2)
            {
                reward -= 100; // Penaliza si la distancia es muy corta
            }
            reward -= 10; // Penaliza si la distancia no aumenta
        }

        // Recompensa adicional si hay una pared entre el agente y el otro jugador
        if (IsWallBeetween(nextCell, OtherPosition))
        {
            reward += 50;
        }

        // Penaliza si el agente se encuentra en una esquina
        if (IsInCorner(nextCell))
        {
            reward -= 1000;
        }

        return reward; // Devuelve la recompensa calculada
    }

    // Calcula el nuevo valor Q basado en la fórmula del Q-learning
    private float UpdateQTable(float currentQ, float maxNextQ, int reward)
    {
        return (1 - _params.alpha) * currentQ + _params.alpha * (reward + _params.gamma * maxNextQ); // Calcula el nuevo valor Q usando la fórmula de Q-learning
    }

    // Guarda la tabla Q en un archivo
    private void SaveQTable()
    {
        string filePath = @"Assets/Scripts/Grupo12/TablaQ.csv"; // Especifica la ruta del archivo
        File.WriteAllLines(filePath, ToCsv(_QTable._tablaQ)); // Escribe la tabla Q en un archivo CSV
    }

    // Convierte la tabla Q a formato CSV
    private static IEnumerable<string> ToCsv<T>(T[,] data, string separator = ";")
    {
        for (int i = 0; i < data.GetLength(0); ++i)
            yield return string.Join(separator, Enumerable.Range(0, data.GetLength(1)).Select(j => data[i, j])); // Convierte cada fila de la tabla Q en una línea de texto separada por el separador especificado
    }
}
