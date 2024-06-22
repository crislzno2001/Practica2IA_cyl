// MyTrainer es el entrenador que usa la tabla Q para aprender y tomar decisiones en un mundo.

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

    // Constructor
    public MyTrainer()
    {
        episodeReturns = new List<float>(); // Inicializa la lista de retornos por episodio
    }

    // Realiza un paso de entrenamiento o ejecución
    public void DoStep(bool train)
    {
        CellInfo agentNextCell;
        CellInfo currentCell = AgentPosition;
        int accion = -1;

        currentState = new QState(AgentPosition, OtherPosition, _worldInfo);
        Debug.Log($"Current State ID: {currentState._idState}");

        do
        {
            if (EscogerNumeroAleatorio())
            {
                accion = AccionAleatoria(); // Escoge una acción aleatoria
            }
            else
            {
                accion = MejorAccion(); // Escoge la mejor acción basada en la tabla Q
            }

            agentNextCell = QMind.Utils.MoveAgent(accion, AgentPosition, _worldInfo);
            if (!agentNextCell.Walkable)
            {
                // Si no se puede caminar, se actualiza el valor Q con una penalización
                float Q = DevolverQ(accion);
                float QNew = ActualizarQ(Q, 0, -10000);
                _QTable.ActualizarQ(accion, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight, QNew);
            }

        } while (!agentNextCell.Walkable); // Repite hasta que el agente se mueva a una celda transitable

        // Actualiza el valor Q para la acción tomada
        float currentQ = DevolverQ(accion);
        float bestNextQ = DevolverMaxQ(agentNextCell);
        int reward = GetReward(agentNextCell, accion);
        float actualizedQ = ActualizarQ(currentQ, bestNextQ, reward);
        _QTable.ActualizarQ(accion, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight, actualizedQ);

        AgentPosition = agentNextCell; // Actualiza la posición del agente
        CellInfo otherCell = QMind.Utils.MoveOther(_navigationAlgorithm, OtherPosition, AgentPosition);
        OtherPosition = otherCell; // Actualiza la posición del otro jugador

        // Actualiza el retorno acumulado
        Return += reward;

        CurrentStep = counter;
        if (OtherPosition == null || CurrentStep == _params.maxSteps || OtherPosition == AgentPosition)
        {
            // Si se cumple alguna condición de fin de episodio, empieza un nuevo episodio
            OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
            NuevoEpisodio();
        }
        else
        {
            counter += 1;
        }
    }

    // Inicia un nuevo episodio
    private void NuevoEpisodio()
    {
        AgentPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al agente
        OtherPosition = _worldInfo.RandomCell(); // Asigna una nueva posición aleatoria al otro jugador
        counter = 0;
        CurrentStep = counter;
        numEpisode++;
        CurrentEpisode = numEpisode;

        // Almacena el retorno del episodio finalizado y calcula el promedio
        if (numEpisode > 1)
        {
            episodeReturns.Add(Return);
            ReturnAveraged = episodeReturns.Average();
        }
        Return = 0; // Reinicia el retorno acumulado para el nuevo episodio

        if (numEpisode % _params.episodesBetweenSaves == 0)
        {
            GuardarTablaQ(); // Guarda la tabla Q cada ciertos episodios
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
        _QTable = new QTable(worldInfo); // Crea una nueva tabla Q
        _QTable.InicializarTabla(); // Inicializa la tabla Q
    }

    // Decide si escoger una acción aleatoria
    private bool EscogerNumeroAleatorio()
    {
        float azar = UnityEngine.Random.Range(0.0f, 1.0f); // Genera un número aleatorio entre 0 y 1
        return azar <= _params.epsilon; // Decide si tomar una acción aleatoria basado en epsilon
    }

    // Escoge una acción aleatoria
    private int AccionAleatoria()
    {
        return UnityEngine.Random.Range(0, 4); // Retorna un número aleatorio entre 0 y 3, representando las cuatro direcciones posibles
    }

    // Escoge la mejor acción basada en la tabla Q
    private int MejorAccion()
    {
        return _QTable.DevolverMejorAccion(currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight); // Devuelve la mejor acción según la tabla Q
    }

    // Devuelve el valor Q para la acción actual
    private float DevolverQ(int accion)
    {
        return _QTable.DevolverQ(accion, currentState._nWalkable, currentState._sWalkable, currentState._eWalkable, currentState._wWalkable, currentState._playerUp, currentState._playerRight); // Devuelve el valor Q actual
    }

    // Devuelve el mejor valor Q para el siguiente estado
    private float DevolverMaxQ(CellInfo nextCell)
    {
        QState nextState = new QState(nextCell, OtherPosition, _worldInfo); // Crea el siguiente estado
        return _QTable.DevolverMejorQ(nextState._nWalkable, nextState._sWalkable, nextState._eWalkable, nextState._wWalkable, nextState._playerUp, nextState._playerRight); // Devuelve el mejor valor Q del siguiente estado
    }

    // Calcula la recompensa basada en la nueva posición del agente y la acción tomada
    private int GetReward(CellInfo nextCell, int accion)
    {
        int distanciaRealInicial = Mathf.Abs(AgentPosition.x - OtherPosition.x) + Mathf.Abs(AgentPosition.y - OtherPosition.y); // Calcula la distancia inicial entre el agente y el otro jugador
        int distanciaRealFinal = Mathf.Abs(nextCell.x - OtherPosition.x) + Mathf.Abs(nextCell.y - OtherPosition.y); // Calcula la distancia final entre el agente y el otro jugador

        int recompensa = 0; // Inicializa la recompensa
        if (nextCell.x == OtherPosition.x && nextCell.y == OtherPosition.y) { return -100; } // Penaliza si el agente se encuentra con el otro jugador
        if (distanciaRealFinal > distanciaRealInicial)
        {
            recompensa += 100; // Recompensa si la distancia aumenta
        }
        else
        {
            if (distanciaRealFinal <= 2)
            {
                recompensa -= 100; // Penaliza si la distancia es muy corta
            }
            recompensa -= 10; // Penaliza si la distancia no aumenta
        }
        if ((nextCell.x == 0 && nextCell.y == 19) || (nextCell.x == 0 && nextCell.y == 0) || (nextCell.x == 19 && nextCell.y == 0) || (nextCell.x == 19 && nextCell.y == 19))
        {
            recompensa -= 1000; // Penaliza si el agente se encuentra en una esquina
        }

        return recompensa; // Devuelve la recompensa calculada
    }

    // Calcula el nuevo valor Q basado en la fórmula del Q-learning
    private float ActualizarQ(float currentQ, float maxNextQ, int reward)
    {
        return (1 - _params.alpha) * currentQ + _params.alpha * (reward + _params.gamma * maxNextQ); // Calcula el nuevo valor Q usando la fórmula de Q-learning
    }

    // Guarda la tabla Q en un archivo
    private void GuardarTablaQ()
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
