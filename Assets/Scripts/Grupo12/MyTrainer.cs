// MyTrainer es el entrenador que usa la tabla Q para aprender y tomar decisiones en un mundo.

using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using QMind;
using QMind.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

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

    private INavigationAlgorithm _navAlgorithm; // Algoritmo de navegación utilizado
    private WorldInfo _worldInfo; // Información sobre el mundo donde se mueve el agente
    private QTable _qTable; // Tabla Q que almacena los valores Q para cada estado y acción
    private QMindTrainerParams _trainerParams; // Parámetros para el entrenador Q-learning
    private int _stepCounter; // Contador de pasos dentro del episodio
    private int _totalEpisodes; // Número total de episodios completados
    private QState _currentState; // Estado actual del agente
    private readonly List<float> _episodeReturns; // Lista para almacenar retornos de cada episodio

    // Constructor
    public MyTrainer()
    {
        _episodeReturns = new List<float>(); // Inicializa la lista de retornos por episodio
    }

    // Realiza un paso de entrenamiento o ejecución
    public void DoStep(bool train)
    {
        _currentState = new QState(AgentPosition, OtherPosition, _worldInfo);
        Debug.Log($"Current State ID: {_currentState._idState}");

        int action;
        CellInfo nextCell;

        do
        {
            action = ShouldChooseRandomAction() ? ChooseRandomAction() : GetBestAction(); // Escoge una acción
            nextCell = QMind.Utils.MoveAgent(action, AgentPosition, _worldInfo);
        } while (!nextCell.Walkable && PenalizeInvalidMove(action));

        // Actualiza el valor Q para la acción tomada
        UpdateQTable(action, nextCell);

        AgentPosition = nextCell; // Actualiza la posición del agente
        OtherPosition = QMind.Utils.MoveOther(_navAlgorithm, OtherPosition, AgentPosition); // Actualiza la posición del otro jugador

        // Actualiza el retorno acumulado
        Return += CalculateReward(nextCell, action);

        // Maneja el fin del episodio
        HandleEndOfEpisode();
    }

    // Penaliza una acción que lleva a una celda no transitable
    private bool PenalizeInvalidMove(int action)
    {
        float currentQ = GetQValue(action);
        float updatedQ = UpdateQValue(currentQ, 0, -10000);
        _qTable.UpdateQ(action, _currentState._nWalkable, _currentState._sWalkable, _currentState._eWalkable, _currentState._wWalkable, _currentState._playerUp, _currentState._playerRight, updatedQ);
        return false;
    }

    // Actualiza la tabla Q para la acción tomada
    private void UpdateQTable(int action, CellInfo nextCell)
    {
        float currentQValue = GetQValue(action);
        float bestNextQValue = GetMaxQValue(nextCell);
        int reward = CalculateReward(nextCell, action);
        float newQValue = UpdateQValue(currentQValue, bestNextQValue, reward);
        _qTable.UpdateQ(action, _currentState._nWalkable, _currentState._sWalkable, _currentState._eWalkable, _currentState._wWalkable, _currentState._playerUp, _currentState._playerRight, newQValue);
    }

    // Maneja las condiciones de fin del episodio
    private void HandleEndOfEpisode()
    {
        CurrentStep = _stepCounter;
        if (OtherPosition == null || CurrentStep == _trainerParams.maxSteps || OtherPosition == AgentPosition)
        {
            OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
            StartNewEpisode();
        }
        else
        {
            _stepCounter++;
        }
    }

    // Inicia un nuevo episodio
    private void StartNewEpisode()
    {
        AgentPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al agente
        OtherPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al otro jugador
        _stepCounter = 0;
        CurrentStep = _stepCounter;
        _totalEpisodes++;
        CurrentEpisode = _totalEpisodes;

        // Almacena el retorno del episodio finalizado y calcula el promedio
        if (_totalEpisodes > 1)
        {
            _episodeReturns.Add(Return);
            ReturnAveraged = _episodeReturns.Average();
        }
        Return = 0; // Reinicia el retorno acumulado para el nuevo episodio

        if (_totalEpisodes % _trainerParams.episodesBetweenSaves == 0)
        {
            SaveQTable(); // Guarda la tabla Q cada ciertos episodios
        }
        OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
    }

    // Inicializa el entrenador con los parámetros y el mundo dados
    public void Initialize(QMindTrainerParams trainerParams, WorldInfo worldInfo, INavigationAlgorithm navAlgorithm)
    {
        _navAlgorithm = Utils.InitializeNavigationAlgo(navAlgorithm, worldInfo); // Inicializa el algoritmo de navegación
        _worldInfo = worldInfo; // Asigna la información del mundo
        AgentPosition = worldInfo.RandomCell(); // Asigna una posición aleatoria al agente
        OtherPosition = worldInfo.RandomCell(); // Asigna una posición aleatoria al otro jugador
        OnEpisodeStarted?.Invoke(this, EventArgs.Empty); // Dispara el evento de inicio de episodio

        _trainerParams = trainerParams; // Asigna los parámetros del entrenador
        _qTable = new QTable(worldInfo); // Crea una nueva tabla Q
        _qTable.InitializeTable(); // Inicializa la tabla Q
    }

    // Decide si escoger una acción aleatoria
    private bool ShouldChooseRandomAction()
    {
        return UnityEngine.Random.Range(0.0f, 1.0f) <= _trainerParams.epsilon; // Decide si tomar una acción aleatoria basado en epsilon
    }

    // Escoge una acción aleatoria
    private int ChooseRandomAction()
    {
        return UnityEngine.Random.Range(0, 4); // Retorna un número aleatorio entre 0 y 3, representando las cuatro direcciones posibles
    }

    // Escoge la mejor acción basada en la tabla Q
    private int GetBestAction()
    {
        return _qTable.GetBestAction(_currentState._nWalkable, _currentState._sWalkable, _currentState._eWalkable, _currentState._wWalkable, _currentState._playerUp, _currentState._playerRight); // Devuelve la mejor acción según la tabla Q
    }

    // Devuelve el valor Q para la acción actual
    private float GetQValue(int action)
    {
        return _qTable.GetQValue(action, _currentState._nWalkable, _currentState._sWalkable, _currentState._eWalkable, _currentState._wWalkable, _currentState._playerUp, _currentState._playerRight); // Devuelve el valor Q actual
    }

    // Devuelve el mejor valor Q para el siguiente estado
    private float GetMaxQValue(CellInfo nextCell)
    {
        QState nextState = new QState(nextCell, OtherPosition, _worldInfo); // Crea el siguiente estado
        return _qTable.GetBestQValue(nextState._nWalkable, nextState._sWalkable, nextState._eWalkable, nextState._wWalkable, nextState._playerUp, nextState._playerRight); // Devuelve el mejor valor Q del siguiente estado
    }

    // Calcula la recompensa basada en la nueva posición del agente y la acción tomada
    private int CalculateReward(CellInfo nextCell, int action)
    {
        int initialDistance = Mathf.Abs(AgentPosition.x - OtherPosition.x) + Mathf.Abs(AgentPosition.y - OtherPosition.y); // Calcula la distancia inicial entre el agente y el otro jugador
        int finalDistance = Mathf.Abs(nextCell.x - OtherPosition.x) + Mathf.Abs(nextCell.y - OtherPosition.y); // Calcula la distancia final entre el agente y el otro jugador

        int reward = 0; // Inicializa la recompensa
        if (nextCell.x == OtherPosition.x && nextCell.y == OtherPosition.y) return -100; // Penaliza si el agente se encuentra con el otro jugador
        if (finalDistance > initialDistance) reward += 100; // Recompensa si la distancia aumenta
        else
        {
            if (finalDistance <= 2) reward -= 100; // Penaliza si la distancia es muy corta
            reward -= 10; // Penaliza si la distancia no aumenta
        }
        if ((nextCell.x == 0 && nextCell.y == 19) || (nextCell.x == 0 && nextCell.y == 0) || (nextCell.x == 19 && nextCell.y == 0) || (nextCell.x == 19 && nextCell.y == 19))
            reward -= 1000; // Penaliza si el agente se encuentra en una esquina

        return reward; // Devuelve la recompensa calculada
    }

    // Calcula el nuevo valor Q basado en la fórmula del Q-learning
    private float UpdateQValue(float currentQ, float maxNextQ, int reward)
    {
        return (1 - _trainerParams.alpha) * currentQ + _trainerParams.alpha * (reward + _trainerParams.gamma * maxNextQ); // Calcula el nuevo valor Q usando la fórmula de Q-learning
    }

    // Guarda la tabla Q en un archivo
    private void SaveQTable()
    {
        string filePath = @"Assets/Scripts/Grupo12/TablaQ.csv"; // Especifica la ruta del archivo
        File.WriteAllLines(filePath, ToCsv(_qTable.QTableValues)); // Escribe la tabla Q en un archivo CSV
    }

    // Convierte la tabla Q a formato CSV
    private static IEnumerable<string> ToCsv<T>(T[,] data, string separator = ";")
    {
        for (int i = 0; i < data.GetLength(0); ++i)
            yield return string.Join(separator, Enumerable.Range(0, data.GetLength(1)).Select(j => data[i, j])); // Convierte cada fila de la tabla Q en una línea de texto separada por el separador especificado
    }
}
