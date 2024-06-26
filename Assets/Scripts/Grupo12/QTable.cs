// QTable gestiona la tabla Q, que guarda los valores Q usados para decidir las acciones del agente.

using NavigationDJIA.World;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QTable : MonoBehaviour
{
    public int _numRows; // Número de acciones posibles
    public int _numCols; // Número de estados posibles
    public float[,] _tablaQ; // Tabla que almacena los valores Q
    public QState[] _listaEstados; // Lista de todos los estados posibles

    // Constructor que inicializa la tabla Q con el mundo dado
    public QTable(WorldInfo world)
    {
        this._numRows = 4; // Cuatro acciones: mover norte, sur, este, oeste
        this._numCols = 16 * 9; // Número de estados posibles
        this._tablaQ = new float[this._numRows, this._numCols]; // Inicializa la tabla Q
        this._listaEstados = new QState[_numCols]; // Inicializa la lista de estados
        initTable(); // Llena la tabla Q con valores iniciales
    }

    // Inicializa la tabla Q con valores predeterminados
    public void initTable()
    {
        for (int i = 0; i < _numRows; i++)
        {
            for (int j = 0; j < _numCols; j++)
            {
                _tablaQ[i, j] = 0; // Inicializa todos los valores Q a -50
            }
        }

        InicializarEstados(); // Crea todos los estados posibles
    }


    // Inicializa los estados posibles en la tabla Q
    public void InicializarEstados()
    {
        int indice = 0;
        bool n, s, e, w;
        for (int i = 0; i < 16; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    // Determina si las direcciones son transitables
                    n = (i / 8 % 2 != 0);
                    s = (i / 4 % 2 != 0);
                    e = (i / 2 % 2 != 0);
                    w = (i % 2 != 0);

                    _listaEstados[indice] = new QState(n, s, e, w, j, k, indice); // Crea un nuevo estado
                    indice++;
                }
            }
        }
    }

    // Devuelve el valor Q para una acción específica
    public float GetQ(int accion, bool n, bool s, bool e, bool w, int up, int right)
    {
        int indice = BuscarEstado(n, s, e, w, up, right); // Busca el estado correspondiente
        return _tablaQ[accion, indice]; // Devuelve el valor Q
    }

    // Devuelve la mejor acción basada en el estado actual
    public int GetBestAction(bool n, bool s, bool e, bool w, int up, int right)
    {
        int indice = BuscarEstado(n, s, e, w, up, right); // Busca el estado correspondiente

        int mejorAccion = 0;
        float mejorQ = _tablaQ[0, indice]; // Inicializa con el primer valor Q

        // Compara para encontrar el mejor valor Q y su acción
        for (int i = 1; i < _numRows; i++)
        {
            if (_tablaQ[i, indice] > mejorQ)
            {
                mejorAccion = i;
                mejorQ = _tablaQ[i, indice];
            }
        }

        return mejorAccion; // Devuelve la mejor acción
    }

    // Devuelve el mejor valor Q basado en el estado actual
    public float GetMaxQ(bool n, bool s, bool e, bool w, int up, int right)
    {
        int indice = BuscarEstado(n, s, e, w, up, right); // Busca el estado correspondiente

        float mejorQ = _tablaQ[0, indice]; // Inicializa con el primer valor Q

        // Compara para encontrar el mejor valor Q
        for (int i = 1; i < _numRows; i++)
        {
            if (_tablaQ[i, indice] > mejorQ)
            {
                mejorQ = _tablaQ[i, indice];
            }
        }

        return mejorQ; // Devuelve el mejor valor Q
    }

    // Actualiza el valor Q para una acción específica
    public void UpdateQTable(int accion, bool n, bool s, bool e, bool w, int up, int right, float actualizedQ)
    {
        int indice = BuscarEstado(n, s, e, w, up, right); // Busca el estado correspondiente
        _tablaQ[accion, indice] = actualizedQ; // Actualiza el valor Q
    }

    // Busca el estado correspondiente en la lista de estados
    private int BuscarEstado(bool n, bool s, bool e, bool w, int up, int right)
    {
        for (int i = 0; i < _listaEstados.Length; i++)
        {
            if (_listaEstados[i]._nWalkable == n &&
                _listaEstados[i]._sWalkable == s &&
                _listaEstados[i]._eWalkable == e &&
                _listaEstados[i]._wWalkable == w &&
                _listaEstados[i]._playerUp == up &&
                _listaEstados[i]._playerRight == right)
            {
                return _listaEstados[i]._idState; // Devuelve el ID del estado encontrado
            }
        }
        return -1; // Estado no encontrado
    }
}
