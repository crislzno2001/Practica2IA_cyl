// QState representa el estado del agente en el mundo.
// Los estados indican si puede moverse en las direcciones norte, sur, este y oeste,
// y también la posición relativa del otro jugador.

using NavigationDJIA.World;
using System.Collections;
using System.Collections.Generic;
using System.Xml.XPath;
using UnityEngine;

public class QState
{
    public bool _nWalkable; // Si se puede caminar hacia el norte
    public bool _sWalkable; // Si se puede caminar hacia el sur
    public bool _eWalkable; // Si se puede caminar hacia el este
    public bool _wWalkable; // Si se puede caminar hacia el oeste

    public int _playerUp;    // Posición del otro jugador en relación al agente (arriba, abajo, mismo nivel)
    public int _playerRight; // Posición del otro jugador en relación al agente (derecha, izquierda, mismo nivel)

    public int _idState;     // Un número único para identificar este estado

    // Constructor que define el estado basado en las posiciones actuales
    public QState(CellInfo AgentPosition, CellInfo OtherPosition, WorldInfo _worldInfo)
    {
        // Definir si las direcciones son transitables
        CellInfo north = QMind.Utils.MoveAgent(0, AgentPosition, _worldInfo);
        _nWalkable = north.Walkable;

        CellInfo south = QMind.Utils.MoveAgent(2, AgentPosition, _worldInfo);
        _sWalkable = south.Walkable;

        CellInfo east = QMind.Utils.MoveAgent(1, AgentPosition, _worldInfo);
        _eWalkable = east.Walkable;

        CellInfo west = QMind.Utils.MoveAgent(3, AgentPosition, _worldInfo);
        _wWalkable = west.Walkable;

        // Definir la posición del otro jugador en relación al agente
        _playerUp = OtherPosition.y > AgentPosition.y ? 0 : (OtherPosition.y < AgentPosition.y ? 1 : 2);
        _playerRight = OtherPosition.x > AgentPosition.x ? 0 : (OtherPosition.x < AgentPosition.x ? 1 : 2);

        _idState = CalculateStateId(); // Calcular un ID único para este estado
    }

    // Constructor alternativo con valores específicos
    public QState(bool n, bool s, bool e, bool w, int up, int right, int idState)
    {
        _nWalkable = n;
        _sWalkable = s;
        _eWalkable = e;
        _wWalkable = w;
        _playerUp = up;
        _playerRight = right;
        _idState = idState;
    }

    // Calcula un ID único basado en las propiedades del estado
    private int CalculateStateId()
    {
        int id = 0;
        if (_nWalkable) id += 1; //Si se puede caminar al norte (_nWalkable), se suma 1 a id.
        if (_sWalkable) id += 2; //Si se puede caminar al sur (_sWalkable), se suma 2 a id.
        if (_eWalkable) id += 4; //Si se puede caminar al este (_eWalkable), se suma 4 a id.
        if (_wWalkable) id += 8; //Si se puede caminar al oeste (_wWalkable), se suma 8 a id.
        id = id * 9 + _playerUp * 3 + _playerRight;
        return id;
    }
}
