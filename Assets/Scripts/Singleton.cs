using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;

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

    private ReversiGraph gameGraph;
    private GameObject gamePiece;
    private Player currentTurn;
    private int aiDifficulty;
    private float aiStartTime;
    private bool aiThinking;
    private bool aiMoveReady;
    private bool aiVSai;
    private bool gameover;

    private delegate Move Search( ReversiGraph graph, Player player, int alpha, int beta, int depth );
    private Search currentSearchingTask;
    IAsyncResult bestMove;

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
		gameGraph = new ReversiGraph();
        gamePiece = (GameObject) Resources.Load( "piece_model" );
        currentTurn = Player.WHITE;
        aiDifficulty = 3;
        aiMoveReady = false;
        aiVSai = false;
        gameover = false;
        aiThinking = false;
    }



    void OnGUI()
    {
        //Rect whiteScoreText = new Rect( 0f, 0f, 5f, 5f );
        //GUI.Label( whiteScoreText, "White:" );
        // Make a background box
        GUI.Box( new Rect( 10, 10, 100, 330 ), "AI Difficulty" );

        for ( int i = 1; i <= 10; ++i )
        {
            //Make buttons for AI Levels
            if ( GUI.Button( new Rect( 20, 10 + ( 30 * i ), 80, 20 ), "Level " + i ) )
            {
                aiDifficulty = i;
            }
        }


        GUI.Box( new Rect( 700, 10, 160, 90 ), "Game Mode: " );
        //Make buttons for AI Levels
        if ( GUI.Button( new Rect( 710, 40, 140, 20 ), "Single Player " ) )
        {
            aiVSai = false;
            if ( currentTurn == Player.WHITE && aiThinking )
            {
                aiThinking = false;
            }
        }
        //Make buttons for AI Levels
        if ( GUI.Button( new Rect( 710, 70, 140, 20 ), "AI vs AI" ) )
        {
            aiVSai = true;
            aiMoveReady = true;
        }

        bool gamefin = IsGameFinished();


        GUI.Box( new Rect( 150, 10, 160, 330 ), "Game Status:" );
        GUI.Label( new Rect( 160, 70, 130, 30 ), "White: " + gameGraph.GetWhiteScore() );
        GUI.Label( new Rect( 160, 90, 130, 30 ), "Black: " + gameGraph.GetBlackScore() );
        GUI.Label( new Rect( 160, 110, 130, 30 ), "Current Turn: " + currentTurn );
        GUI.Label( new Rect( 160, 130, 130, 30 ), "AI Difficulty: " + aiDifficulty );

        //if ( !gamefin && ( currentTurn == Player.BLACK || aiVSai ) )
        if( aiThinking )
        {
            GUI.Label( new Rect( 180, 150, 130, 30 ), "AI Thinking... " );

            int thinkingTime = (int)( Time.time - aiStartTime );
            int minutes = thinkingTime / 60;
            int seconds = thinkingTime % 60;
            //string time = String.Format( "time: ( %f.0:%
            GUI.Label( new Rect( 180, 170, 130, 30 ), "Time: " + minutes.ToString( "n0" ) + ":" + ( seconds < 10f ? "0" : "" ) + seconds.ToString( "n0" ) );

            //if ( !aiMoveReady )
            //{
            //    aiMoveReady = true;
            //}
        }

        if ( gamefin )
        {
            int white = gameGraph.GetWhiteScore();
            int black = gameGraph.GetBlackScore();

            if( white == black )
            {
                GUI.Label( new Rect( 180, 250, 130, 30 ), "tie game!" );
            }
            else
            {
                GUI.Label( new Rect( 180, 250, 130, 30 ), "" + ( white > black ? "White " : "Black " ) + "wins!" );
            }
        }
    }


    public void Update()
    {
        if ( IsGameFinished() ) return;

        if ( aiThinking )
        {
            print( "AI is thinking" );
            if ( bestMove.IsCompleted )
            {

                //async result completed, retrieve the result by ending the task on our delegate
                Move best = currentSearchingTask.EndInvoke( bestMove );

                //perform the move for the current player
                performMove( gameGraph, gameGraph.Cells[best.Cell], currentTurn );

                //turn finished, set it back to white
                aiThinking = false;
                currentTurn = GetOppositePlayer( currentTurn );

                if ( aiVSai ) aiMoveReady = true;
            }
        }

        else if ( aiMoveReady )
        {
            //begin finding the best move using negamax asyncronously
            AsyncronousAIMove( gameGraph, currentTurn );
        }
    }


    private bool debugTraversal = false;

    public void Debug()
    {
        debugTraversal = true;
		foreach ( ReversiGraph.GridCell cell in gameGraph.Cells.Values )
        {
            print( "name: " + cell.Name + " state: " + cell.State );
        }
        print( "white: " + gameGraph.GetWhiteScore() + " black: " + gameGraph.GetBlackScore() + " empty: " + gameGraph.GetEmptyCount() );
    }


    /**
     * this method will be invoked by the script attached to a square on the Reversi board
     */
    public void itemClicked( string name )
    {
        if ( aiVSai || currentTurn == Player.BLACK ) return;

		ReversiGraph.GridCell selected = gameGraph.Cells[name];

		if ( isValidMove( gameGraph, selected, Player.WHITE ) )
        {
			performMove( gameGraph, selected, Player.WHITE );

            //now process a move for the AI
            currentTurn = Player.BLACK;
            aiMoveReady = true;
        }
        else
        {
            print( "NOT VALID LOLZ" );
        }
    }




    /**
     * uses the AI to decide a move for the given player
     */
    private void AsyncronousAIMove( ReversiGraph graph, Player player )
    {
        //verify it is this player's turn
        if ( currentTurn != player || IsGameFinished() ) return;

        //evaluate the moves and determine the best one
        //Func<ReversiGraph, Player, int, int, int, Move> searcher = ( ReversiGraph g, Player p, int a, int b, int diff ) => negamax( graph, player, Int32.MinValue, Int32.MaxValue, aiDifficulty )

        //make sure to toggle the states properly so that we don't accidentally start another async task
        aiMoveReady = false;
        aiThinking = true;

        //set up our delegate instance
        currentSearchingTask = ( ( ReversiGraph g, Player p, int a, int b, int ai ) => negamax( g, p, a, b, ai ) );

        //set our AI start time
        aiStartTime = Time.time;

        //invoke the delegate asyncronously and store the reference to the AsyncResult used to retrieve the result
        bestMove = currentSearchingTask.BeginInvoke( graph, player, Int32.MinValue, Int32.MaxValue, aiDifficulty, null, null );


        //Move bestMove = negamax( graph, player, Int32.MinValue, Int32.MaxValue, aiDifficulty );
        //performMove( graph, graph.Cells[bestMove.Cell], player );
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
            return new Move( null, graph.DetermineAdvantageFor( player ) );
        }

        //if we haven't reached the bottom of the traversal tree and the graph has possible moves, we need to check them further.
        HashSet<ReversiGraph.GridCell> moves = GetAllPossibleMoves( graph, player );
        Move bestMove = new Move( null, alpha );

        //we have moves available! that other guy is going down
        if ( moves.Count > 0 )
        {
            //to add an element of randomness, I'm going to alter this algorithm to give the AI a way to randomly choose between equal moves
            ArrayList bestMoves = new ArrayList();

            foreach ( ReversiGraph.GridCell cell in moves )
            {
                //we need to duplicate our graph for the recursive call, so that our current board isn't maniplulated. luckily this is efficient
                //because we don't copy all the edges in every graph clone, we use a lookup table for that.
                ReversiGraph cloned = graph.Clone();

                //perform the move on the cloned graph using the current player
                performMove( cloned, cloned.Cells[cell.Name], player );

                //use recursion to determine the opponents next best move
				Player oppPlayer = GetOppositePlayer( player );

				//extra comment for debugging issue (monodevelop sucks)
                Move opponentsBestMove = negamax( cloned, oppPlayer, -beta, -alpha, depth - 1 );

                //we flip our opponents score, to determine how much we like it. if the move is better for us, we save it.
                int score = -opponentsBestMove.Score;
                if ( alpha < score )
                {
                    alpha = score;
                    bestMove = new Move( cell.Name, score );

                    //storing a list of equal moves will give us an element of randomness. we still store bestMove though in the event of pruning
                    bestMoves.Clear();
                    bestMoves.Add( bestMove );
                }
                else if ( alpha == score )
                {
                    bestMoves.Add( new Move( cell.Name, score ) );
                }

                //this is the alpha beta pruning. If this statement evaluates to true, it means we cannot possibly find a more optimum move down the recursive tree in this direction
                if ( alpha >= beta )
                {
                    return bestMove;
                }
            }

            if ( bestMoves.Count > 1 )
            {
                //bestMove = (Move) bestMoves[UnityEngine.Random.Range( 0, bestMoves.Count - 1 )];
                bestMove = (Move) bestMoves[ new System.Random().Next( bestMoves.Count ) ];
            }
        }

        //if we have no moves available and given that the depth is above 0, it is the other player's turn
        else
        {
            //check for the opposite player
            moves = GetAllPossibleMoves( graph, GetOppositePlayer( player ) );

            //our opponent is able to move
            if ( moves.Count > 0 )
            {
                //we don't need to clone the graph, since we made no changes to it, so recursively call negamax as the opposite player
                Move opponentsBestMove = negamax( graph, GetOppositePlayer( player ), -beta, -alpha, depth - 1 );

                //we need to invert the returned move, since we want the opposite outcome as our opponent
                bestMove = new Move( opponentsBestMove.Cell, -opponentsBestMove.Score );

            }
            //if no moves are available for him either, the game is over and we determine the winner
            else
            {
                int difference = graph.DetermineAdvantageFor( player );

                //if the advantage is positive, we win!
                if ( difference > 0 )
                {
                    bestMove = new Move( null, Int32.MaxValue );
                }
                //tie game?
                else if ( difference == 0 )
                {
                    bestMove = new Move( null, 0 );
                }
                //if we get a negative number, we will lose this game.
                else
                {
                    bestMove = new Move( null, Int32.MinValue );
                }
            }
        }

        return bestMove;
    }


    /**
     * determines if the given GridCell is a valid move for the given player
     */
    private bool isValidMove( ReversiGraph graph, ReversiGraph.GridCell given, Player player )
    {
        if ( given.State != ReversiGraph.CellState.EMPTY ) return false;

        ReversiGraph.CellState seeking = GetSeekingState( player );

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
        //if ( currentTurn != player ) return;

        //instantiate a new hashset which will be used to keep track of any grid cells which need to be flipped as a result of this move
        HashSet<ReversiGraph.GridCell> flipsNeeded = new HashSet<ReversiGraph.GridCell>();

        //determine which color we are seeking
        ReversiGraph.CellState seeking = GetSeekingState( player );

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
            cell.Flip( graph == gameGraph );
        }

        //place a piece at the current position, only if we are working with the actual game board
		if (gameGraph == graph)
		{
			Quaternion rotation = (player == Player.WHITE ? Quaternion.identity : new Quaternion (0f, 0f, 3.1415f, 0f));
			GameObject newPiece = (GameObject)Instantiate (gamePiece, given.SpawnPoint, rotation);
			given.State = seeking;
			given.Model = newPiece;
		}
    }


    /**
     * returns the cell state we intend to find for traversals given the player that is performing the current move
     */
    private ReversiGraph.CellState GetSeekingState( Player player )
    {
        return ( player == Player.WHITE ? ReversiGraph.CellState.WHITE : ReversiGraph.CellState.BLACK );
    }


    /**
     * returns the cell state we intend to find for traversals given the player that is performing the current move
     */
    private Player GetOppositePlayer( Player player )
    {
        return player == Player.WHITE ? Player.BLACK : Player.WHITE;
    }


    /**
     * returns the true if the game is finished
     */
    public bool IsGameFinished()
    {
        if ( gameover ) return true;

        //the gameGraph can quickly determine if it is in a final state
        if ( gameGraph.IsFinished() )
        {
            gameover = true;
        }
        
        //otherwise we resort to scouring the graph twice to see if there are no more moves possible
        else if( ( GetAllPossibleMoves( gameGraph, Player.WHITE ).Count < 1 ) && ( GetAllPossibleMoves( gameGraph, Player.BLACK ).Count < 1 ) ) 
        {
            gameover = true;
        }

        return gameover;
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

        
        /**
         * determines the advantage that the given player has over the opposite player
         *  @returns the number of squares owned by the given player, less the number owned by the opposite player
         */
        public int DetermineAdvantageFor( Player player )
        {
            if ( dirty ) Examine();
            int difference = white - black;
            if ( player == Player.BLACK ) difference *= -1;
            return difference;
        }

#endregion


        #region Graph inner-class GridCell

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

                //we actually don't need to copy the model to any other graph but the main one
                //this.model = other.model;
            }

            public GridCell Clone()
            {
                return new GridCell( this );
            }

            /**
             * inverts the cell's state from one player to the other and modified the model to match the owning player
             */
            public void Flip( bool models )
            {
                if ( State == CellState.EMPTY )
                {
                    return;
                }
                else if ( State == ReversiGraph.CellState.WHITE )
                {
                    State = ReversiGraph.CellState.BLACK;
                    if ( models )
                    {
                        model.transform.rotation = new Quaternion( 0f, 0f, 3.1415f, 0f );
                    }
                }
                else
                {
                    State = ReversiGraph.CellState.WHITE;
                    if ( models )
                    {
                        model.transform.rotation = Quaternion.identity;
                    }
                }
            }
        #endregion
        }

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
