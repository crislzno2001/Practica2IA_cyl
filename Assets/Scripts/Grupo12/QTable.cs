// QTable gestiona la tabla Q, que guarda los valores Q usados para decidir las acciones del agente.

using NavigationDJIA.World;
using System.Collections.Generic;
using UnityEngine;

public class QTable : MonoBehaviour
{
    private const int NumActions = 4; // Número de acciones posibles
    private const int NumStates = 16 * 9; // Número de estados posibles
    private readonly float[,] _qTable; // Tabla que almacena los valores Q
    private readonly QState[] _stateList; // Lista de todos los estados posibles

    // Constructor que inicializa la tabla Q con el mundo dado
    public QTable(WorldInfo world)
    {
        _qTable = new float[NumActions, NumStates]; // Inicializa la tabla Q
        _stateList = new QState[NumStates]; // Inicializa la lista de estados
        InitializeTable(); // Llena la tabla Q con valores iniciales
    }

    // Propiedad para acceder al número de acciones
    public int ActionsCount => NumActions;

    // Propiedad para acceder a la tabla Q
    public float[,] QTableValues => _qTable;

    // Inicializa la tabla Q con valores predeterminados
    public void InitializeTable()
    {
        // Inicializa todos los valores Q a 0
        System.Array.Clear(_qTable, 0, _qTable.Length);
        InitializeStates(); // Crea todos los estados posibles
    }

    // Inicializa los estados posibles en la tabla Q
    private void InitializeStates()
    {
        int index = 0;
        for (int i = 0; i < 16; i++)
        {
            bool n = (i & 8) != 0; // i / 8 % 2 != 0
            bool s = (i & 4) != 0; // i / 4 % 2 != 0
            bool e = (i & 2) != 0; // i / 2 % 2 != 0
            bool w = (i & 1) != 0; // i % 2 != 0

            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    _stateList[index] = new QState(n, s, e, w, j, k, index); // Crea un nuevo estado
                    index++;
                }
            }
        }
    }

    // Devuelve el valor Q para una acción específica
    public float GetQValue(int action, bool n, bool s, bool e, bool w, int up, int right)
    {
        int index = FindStateIndex(n, s, e, w, up, right); // Busca el estado correspondiente
        return _qTable[action, index]; // Devuelve el valor Q
    }

    // Devuelve la mejor acción basada en el estado actual
    public int GetBestAction(bool n, bool s, bool e, bool w, int up, int right)
    {
        int index = FindStateIndex(n, s, e, w, up, right); // Busca el estado correspondiente

        int bestAction = 0;
        float bestQValue = _qTable[0, index]; // Inicializa con el primer valor Q

        // Compara para encontrar el mejor valor Q y su acción
        for (int i = 1; i < NumActions; i++)
        {
            if (_qTable[i, index] > bestQValue)
            {
                bestAction = i;
                bestQValue = _qTable[i, index];
            }
        }

        return bestAction; // Devuelve la mejor acción
    }

    // Devuelve el mejor valor Q basado en el estado actual
    public float GetBestQValue(bool n, bool s, bool e, bool w, int up, int right)
    {
        int index = FindStateIndex(n, s, e, w, up, right); // Busca el estado correspondiente

        float bestQValue = _qTable[0, index]; // Inicializa con el primer valor Q

        // Compara para encontrar el mejor valor Q
        for (int i = 1; i < NumActions; i++)
        {
            if (_qTable[i, index] > bestQValue)
            {
                bestQValue = _qTable[i, index];
            }
        }

        return bestQValue; // Devuelve el mejor valor Q
    }

    // Actualiza el valor Q para una acción específica
    public void UpdateQ(int action, bool n, bool s, bool e, bool w, int up, int right, float updatedQ)
    {
        int index = FindStateIndex(n, s, e, w, up, right); // Busca el estado correspondiente
        _qTable[action, index] = updatedQ; // Actualiza el valor Q
    }

    // Busca el estado correspondiente en la lista de estados
    private int FindStateIndex(bool n, bool s, bool e, bool w, int up, int right)
    {
        for (int i = 0; i < _stateList.Length; i++)
        {
            QState state = _stateList[i];
            if (state._nWalkable == n &&
                state._sWalkable == s &&
                state._eWalkable == e &&
                state._wWalkable == w &&
                state._playerUp == up &&
                state._playerRight == right)
            {
                return state._idState; // Devuelve el ID del estado encontrado
            }
        }
        Debug.LogError("Estado no encontrado"); // Registra un error si el estado no se encuentra
        return -1; // Estado no encontrado
    }
}
