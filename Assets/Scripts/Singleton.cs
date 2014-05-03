using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class Singleton : MonoBehaviour
{
    
    public enum Direction
    {
        NONE,
        N,  //north
        S,  //south
        E,  //east
        W,  //west
        NE, //north-east
        NW, //north-west
        SE, //south-east
        SW, //south-west
    }

    public enum Player
    {
        WHITE,
        BLACK,
    }

    public static Singleton i;

    private ReversiGraph graph;
    private GameObject gamePiece;
    private Player currentTurn;
    private int aiDifficulty;

    /**
     * presumably, this method is called when the GameObject it is attached to is brought to life in a scene
     *  - this logic is brought to you by James Boddie, and basically prevents the GameObject itself from being instantiated
     *    more than once, and also prevents it from being destroyed when and if scenes change.
     *    
     * NOTE: ahh- from discussion board regarding this topic:
     * All Awakes run before any Starts run before any Updates. You usually use Awake if you need something initialized so somebody's Start function can use it.
     */
    public void Awake()
    {
        if ( i != null )
            GameObject.Destroy( i );
        else
            i = this;
        DontDestroyOnLoad( this );
    }

    /**
     * initialize the graph
     */
    public void Start()
    {
        graph = new ReversiGraph();
        gamePiece = (GameObject) Resources.Load( "piece_model" );
        currentTurn = Player.WHITE;
        aiDifficulty = 5;
    }

    private bool debugTraversal = false;

    public void Debug()
    {
        debugTraversal = true;
        foreach ( ReversiGraph.GridCell cell in graph.Cells.Values )
        {
            print( "name: " + cell.Name + " state: " + cell.State );
        }
    }


    /**
     * this method will be invoked by the script attached to a square on the Reversi board
     */
    public void itemClicked( string name )
    {
        print( "DEBUG: " + name + " STATE: " + graph.Cells[name].State );

        ReversiGraph.GridCell selected = graph.Cells[name];

        //if ( isValidMove( selected, Player.WHITE ) )
        if ( isValidMove( graph, selected, currentTurn ) )
        {
            print( "VALID YAY!" );
            //performMove( selected, Player.WHITE );
            performMove( graph, selected, currentTurn );
        }
        else
        {
            print( "NOT VALID LOLZ" );
        }
    }




    /**
     * uses the AI to decide a move for the given player
     */
    private void AIMove( ReversiGraph graph, Player player )
    {
        //verify it is this player's turn
        if ( currentTurn != player ) return;

        //get a list of all possible moves
        HashSet<ReversiGraph.GridCell> moves = GetAllPossibleMoves( graph, player );

        //evaluate the moves and determine the best one
        Move bestMove = negamax( graph, player, Int32.MinValue, Int32.MaxValue, aiDifficulty );
    }



    /**
     * retrieves a set of all possible moves for the given player
     */
    private HashSet<ReversiGraph.GridCell> GetAllPossibleMoves( ReversiGraph graph, Player player )
    {
        HashSet<ReversiGraph.GridCell> allMoves = new HashSet<ReversiGraph.GridCell>();
        foreach ( ReversiGraph.GridCell cell in graph.Cells.Values )
        {
            if ( isValidMove( graph, cell, player ) )
            {
                allMoves.Add( cell );
            }
        }
        return allMoves;
    }


    /**
     * performs the negamax algorithm using alpha/beta pruning to determine the best possible move
     */
    private Move negamax( ReversiGraph graph, Player player, int alpha, int beta, int depth )
    {
        if ( graph == null ) return null;

        if ( depth <= 0 || graph.IsFinished() )
        {
            int score = graph.GetWhiteScore() - graph.GetBlackScore();
            if( player == Player.BLACK ) score *= -1;
            return new Move( null, score );
        }

        //if we haven't reached the bottom of the traversal tree and the graph has possible moves, we need to check them further.
        HashSet<ReversiGraph.GridCell> moves = GetAllPossibleMoves( graph, player );
        Move bestMove = null;

        //more code here
    }


    /**
     * determines if the given GridCell is a valid move for the given player
     */
    private bool isValidMove( ReversiGraph graph, ReversiGraph.GridCell given, Player player )
    {
        if ( given.State != ReversiGraph.CellState.EMPTY ) return false;

        ReversiGraph.CellState seeking = getSeekingState( player );

        //loop through each possible direction to determine if a move is possible
        foreach ( Direction direction in (Direction[]) Enum.GetValues( typeof( Direction ) ) )
        {
            if ( given.Edges.ContainsKey( direction ) )
            {
                //traverse the current direction, a move is valid only if we find another same state cell AT LEAST two cells away.
                int depth = graph.Traverse( given, seeking, direction, null, 0 );
                if ( depth > 1 )
                {
                    return true;
                }
            }
        }

        return false;
    }


    /**
     * performs a move for the given player
     */
    private void performMove( ReversiGraph graph, ReversiGraph.GridCell given, Player player )
    {
        if ( given.State != ReversiGraph.CellState.EMPTY ) return;
        if ( currentTurn != player ) return;

        //instantiate a new hashset which will be used to keep track of any grid cells which need to be flipped as a result of this move
        HashSet<ReversiGraph.GridCell> flipsNeeded = new HashSet<ReversiGraph.GridCell>();

        //determine which color we are seeking
        ReversiGraph.CellState seeking = getSeekingState( player );

        //loop through each possible direction and flip any necessary pieces
        foreach ( Direction direction in (Direction[]) Enum.GetValues( typeof( Direction ) ) )
        {
            //traverse the given direction if needed
            if ( given.Edges.ContainsKey( direction ) )
            {
                //traverse the direction
                graph.Traverse( given, seeking, direction, flipsNeeded, 0 );
            }
        }

        //we don't need to flip the current cell (although the code is structured such that this would not cause an issue)
        flipsNeeded.Remove( given );

        //go through and flip each of the cells which need to be flipped
        foreach ( ReversiGraph.GridCell cell in flipsNeeded )
        {
            cell.Flip();
        }

        //place a piece at the current position
        Quaternion rotation = ( player == Player.WHITE ? Quaternion.identity : new Quaternion( 0f, 0f, 3.1415f, 0f ) );
        GameObject newPiece = (GameObject) Instantiate( gamePiece, given.SpawnPoint, rotation );
        given.State = seeking;
        given.Model = newPiece;

        currentTurn = ( currentTurn == Player.WHITE ? Player.BLACK : Player.WHITE );
    }


    /**
     * returns the cell state we intend to find for traversals given the player that is performing the current move
     */
    public ReversiGraph.CellState getSeekingState( Player player )
    {
        return ( player == Player.WHITE ? ReversiGraph.CellState.WHITE : ReversiGraph.CellState.BLACK );
    }



    #region ReversiGraph class

    /**
     * a graph class to represent the game board, cells, and traversal logic within the game of Reversi
     */
    private class ReversiGraph
    {

        #region Graph Properties, Constructors

        public enum CellState
        {
            EMPTY,
            BLACK,
            WHITE
        }

        //a public property for the cells, with only getter access
        public Dictionary<string, GridCell> Cells
        {
            get;
            private set;
        }

        #region private internal items which allow us to easily track the state of our board

        //determines if the board needs to be re-examined
        private bool dirty;

        //number of white cells
        private int white;

        //number of black cells
        private int black;

        //number of empty cells
        private int empty;

        #endregion

        //constructors are good!
        public ReversiGraph()
        {
            Cells = new Dictionary<string, GridCell>();
            dirty = true;

            //create a temporary array for the individual cells, for use only while constructing the graph
            GridCell[][] cellArr = new GridCell[8][];

            //loop through to create the 64 cells necessary (8x8).
            for ( int i = 0; i < 8; ++i )
            {
                int row = i + 1;
                cellArr[i] = new GridCell[8];
                for ( int j = 0; j < 8; ++j )
                {
                    char letter = (char) ( j + 65 );
                    string name = "_" + letter + row;
                    cellArr[i][j] = new GridCell( name );
                    cellArr[i][j].SpawnPoint = new Vector3( -3.5f + ( j * 1f ), 1f, 3.5f - ( i * 1f ) );
                    Cells.Add( name, cellArr[i][j] );
                }
            }

            //initialize states and models of starting positions
            cellArr[3][3].State = ReversiGraph.CellState.BLACK;
            cellArr[3][3].Model = GameObject.Find( "Piece_D4" );

            cellArr[4][4].State = ReversiGraph.CellState.BLACK;
            cellArr[4][4].Model = GameObject.Find( "Piece_E5" );

            cellArr[3][4].State = ReversiGraph.CellState.WHITE;
            cellArr[3][4].Model = GameObject.Find( "Piece_E4" );

            cellArr[4][3].State = ReversiGraph.CellState.WHITE;
            cellArr[4][3].Model = GameObject.Find( "Piece_D5" );

            /**
             * set up edges. Each GridCell actually uses a static map of Direction->string which will give us the ability to
             * quickly clone the board for recursive negamaxing without having to duplicate every edge. We simply shallow copy
             * the edge list, and use each graph's own string->GridCell map to look up the correct GridCell instance as we traverse.
             */
            for ( int i = 0; i < 8; ++i )
            {
                for ( int j = 0; j < 8; ++j )
                {
                    GridCell current = cellArr[i][j];

                    //NW, N, NE
                    if ( i > 0 )
                    {
                        //NW
                        if ( j > 0 )
                        {
                            current.Edges.Add( Direction.NW, cellArr[i - 1][j - 1].Name );
                        }

                        //N
                        current.Edges.Add( Direction.N, cellArr[i - 1][j].Name );

                        //NE
                        if ( j < 7 )
                        {
                            current.Edges.Add( Direction.NE, cellArr[i - 1][j + 1].Name );
                        }
                    }

                    //W
                    if ( j > 0 )
                    {
                        current.Edges.Add( Direction.W, cellArr[i][j - 1].Name );
                    }

                    //E
                    if ( j < 7 )
                    {
                        current.Edges.Add( Direction.E, cellArr[i][j + 1].Name );
                    }


                    //SW, S, SE
                    if ( i < 7 )
                    {
                        //SW
                        if ( j > 0 )
                        {
                            current.Edges.Add( Direction.SW, cellArr[i + 1][j - 1].Name );
                        }

                        //S
                        current.Edges.Add( Direction.S, cellArr[i + 1][j].Name );

                        //SE
                        if ( j < 7 )
                        {
                            current.Edges.Add( Direction.SE, cellArr[i + 1][j + 1].Name );
                        }
                    }
                }
            }
        }

        private ReversiGraph( ReversiGraph other )
        {
            //for a copy constructor, we create a fresh instance of the Cells map
            this.Cells = new Dictionary<string, GridCell>();
            this.dirty = true;

            //add a clone of each cell to our map
            foreach ( GridCell cell in other.Cells.Values )
            {
                Cells.Add( cell.Name, cell.Clone() );
            }
        }

        #endregion


        #region traversal function
        /**
         * recursively traverses the graph starting from the given GridCell, moving only in the indicated direction,
         * with the objective of finding the first GridCell in the given direction which is in the requested CellState
         *
         * @params
         *      * GridCell current              - the current cell in the traversal
         *      * CellState requested           - the CellState being sought after
         *      * Direction direction           - the direction of the traversal
         *      * HashSet<GridCell> flipPieces  - any cells which may need to be flipped at the end of the traversal will be added to this list, may be null
         *      * int depth                     - the depth traversed so far
         *                                      - should only be called with depth = 0
         *                              
         * @return the number of cells traversed in the given direction before the first matching state cell is found (inclusive)
         */
        public int Traverse( GridCell current, CellState requested, Direction direction, HashSet<GridCell> flipPieces, int depth )
        {
            //mark this graph for re-examination if needed
            dirty = true;

            //error checking is always advisable
            if ( current == null || depth < 0 )
            {
                return 0;
            }

            //did we find what we're looking for?
            if ( current.State == requested )
            {
                return depth;
            }

            //if we're at an empty square
            if ( current.State == CellState.EMPTY && depth != 0 )
            {
                return 0;
            }


            //further down the rabbit hole?
            if ( current.Edges.ContainsKey( direction ) )
            {
                int result = Traverse( Cells[ current.Edges[direction] ], requested, direction, flipPieces, depth + 1 );
                if ( flipPieces != null && result > 0 )
                {
                    flipPieces.Add( current );
                }
                return result;
            }
            else
            {
                return 0;
            }
        }

        #endregion


        #region clone function

        public ReversiGraph Clone()
        {
            return new ReversiGraph( this );
        }

        #endregion


        #region other functions

        /**
         * examines the current state of the graph
         */
        public void Examine()
        {
            black = 0;
            white = 0;
            empty = 0;

            foreach ( GridCell cell in Cells.Values )
            {
                switch ( cell.State )
                {
                    case CellState.BLACK:
                        black++;
                        break;

                    case CellState.WHITE:
                        white++;
                        break;

                    case CellState.EMPTY:
                        empty++;
                        break;
                }
            }

            dirty = false;
        }


        /**
         * determines if the board is in a final state
         */
        public bool IsFinished()
        {
            if ( dirty ) Examine();
            return empty == 0 || black == 0 || white == 0;
        }


        /**
         * determines the current score of the white player
         */
        public int GetWhiteScore()
        {
            if ( dirty ) Examine();
            return white;
        }


        /**
         * determines the current score of the black player
         */
        public int GetBlackScore()
        {
            if ( dirty ) Examine();
            return black;
        }


        /**
         * determines the number of empty spaces
         */
        public int GetEmptyCount()
        {
            if ( dirty ) Examine();
            return empty;
        }


        #region Graph inner-classes; GridCell and Edge

        /**
         * this class represents an individual square on the board, more formally: V
         */
        public class GridCell
        {
            public string Name
            {
                get;
                private set;
            }

            //a reference to the model which appears on this cell of the board
            private GameObject model;

            //a public accessor property, the only reason I have to have this seperate is because I'm defining my own set body, so must define get as well
            public GameObject Model
            {
                get
                {
                    return model;
                }
                set
                {
                    if ( model == null )
                    {
                        model = value;
                    }
                }
            }

            //the current state of the cell
            public CellState State
            {
                get;
                set;
            }

            //the center of the GridCell object, in game space, where pieces should be placed
            public Vector3 SpawnPoint
            {
                get;
                set;
            }

            //a static collection of edges
            private Dictionary<Direction, string> edges;

            //a getter for the edges
            public Dictionary<Direction, string> Edges
            {
                get
                {
                    return edges;
                }
            }

            //the only public constructor requires a name which should match the name of the cell on the actual Reversi board
            public GridCell( string name )
            {
                this.Name = name;
                edges = new Dictionary<Direction, string>();
            }

            //a private copy constructor
            private GridCell( GridCell other )
            {
                this.Name = other.Name;
                this.SpawnPoint = new Vector3( other.SpawnPoint.x, other.SpawnPoint.y, other.SpawnPoint.z );
                this.State = other.State;

                //make sure to only shallow copy the list of edges, we'll use the graph this cell belongs to in order find the proper instance
                this.edges = other.edges;

                //we actually don't want to copy the model to any other graph but the main one
                //this.model = other.model;
            }

            public GridCell Clone()
            {
                return new GridCell( this );
            }

            /**
             * inverts the cell's state from one player to the other and modified the model to match the owning player
             */
            public void Flip()
            {
                if ( State == CellState.EMPTY )
                {
                    return;
                }
                else if ( State == ReversiGraph.CellState.WHITE )
                {
                    State = ReversiGraph.CellState.BLACK;
                    if ( model != null )
                    {
                        model.transform.rotation = new Quaternion( 0f, 0f, 3.1415f, 0f );
                    }
                }
                else
                {
                    State = ReversiGraph.CellState.WHITE;
                    if ( model != null )
                    {
                        model.transform.rotation = Quaternion.identity;
                    }
                }
            }
        }

        /**
         * this class represents a connection to another square on the board, more formally: E
         */
        //public class Edge
        //{
        //    public Direction Direction
        //    {
        //        get;
        //        private set;
        //    }

        //    public string Target
        //    {
        //        get;
        //        private set;
        //    }

        //    public Edge( Direction dir, string target )
        //    {
        //        this.Direction = dir;
        //        this.Target = target;
        //    }


        //}

        #endregion
    }


    #endregion


    #region Move class

    private class Move
    {
        //public Direction Direction
        //{
        //    get;
        //    private set;
        //}

        public string Cell
        {
            get;
            private set;
        }

        public int Score
        {
            get;
            private set;
        }

        //public Move( Direction dir, string cell, int score )
        public Move( string cell, int score )
        {
            //this.Direction = dir;
            this.Cell = cell;
            this.Score = score;
        }
    }

    #endregion
}
