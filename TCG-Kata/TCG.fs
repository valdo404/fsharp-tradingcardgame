module TCG

// https://github.com/bkimminich/kata-tcg

type Card = int
type PlayerChosen = Player1 | Player2


type CreateGame = {
    DeckPlayer1 : Card list
    DeckPlayer2 : Card list
}

type BeginGame = {
    FirstPlayer: PlayerChosen // Random
    PickedCard: Card // Random
}

type Cmd =
    | CreateGame of CreateGame
    | BeginGame of BeginGame
    | StartNewTurn // hidden picked card (random)
    | PlayCard of Card
    | EndTurn

type PlayerPickedACard = {
    Player : PlayerChosen
    Card : Card
}

type PlayerHealthReduced = { 
    Player: PlayerChosen
    HealthReduced: int
}

type HandInitiated = {
    Player : PlayerChosen
    Card1 : Card
    Card2 : Card
    Card3 : Card
}

type GameCreated = {
    DeckPlayer1 : Card list
    DeckPlayer2 : Card list
}

type Evt =
    | GameCreated of GameCreated
    | FirstPlayerChosen of PlayerChosen
    | HandInitiated of HandInitiated
    | PlayerPickedACard of PlayerPickedACard
    | PlayerGotMana of PlayerChosen
    | PlayerGotManaMax of PlayerChosen
    | PlayerEndedTurn of PlayerChosen
    | PlayerPlayedCard of Card
    | PlayerHealthReduced of PlayerHealthReduced

type Player = {
    Deck : Card list
    Hand : Card list
    Mana : int
    ManaMax : int
    Health : int
}

type Game =  {
    Player1 : Player
    Player2 : Player
    Current : PlayerChosen option
}

type Error = {
    Message: string
}

let pickCardFromDeck player card =
    let rec removeFirst deck =
        match deck with
        | [] -> []
        | head::tail when head = card -> tail
        | head::tail -> head::(removeFirst tail)
    
    { player with
        Hand = player.Hand@[card]
        Deck = removeFirst player.Deck }

let initialDeck = [0;0;1;1;2;2;2;3;3;3;3;4;4;4;5;5;6;6;7;8]

let hydrate (events: Evt list) : Game =  
    
    let handleEvent state evt =
        match evt with
        | GameCreated gameCreated ->
            { state with
                Player1 = { state.Player1 with Deck = gameCreated.DeckPlayer1 }
                Player2 = { state.Player2 with Deck = gameCreated.DeckPlayer1 }
            }
            
        | FirstPlayerChosen player -> { state with Current = Some player }
        | HandInitiated handInitiated ->
            let pickCards player =
                [handInitiated.Card1; handInitiated.Card2; handInitiated.Card3]
                |> List.fold pickCardFromDeck player
            
            match handInitiated.Player with
            | Player1 -> { state with Player1 = pickCards state.Player1 }
            | Player2 -> { state with Player2 = pickCards state.Player2 }
            
        | PlayerGotMana player ->
            match player with
            | Player1 -> { state with Player1 = { state.Player1 with ManaMax = state.Player1.ManaMax + 1 } }
            | Player2 -> { state with Player2 = { state.Player2 with ManaMax = state.Player2.ManaMax + 1 } }
            
        | PlayerGotManaMax player -> 
            match player with
            | Player1 -> { state with Player1 = { state.Player1 with Mana = state.Player1.ManaMax } }
            | Player2 -> { state with Player2 = { state.Player2 with Mana = state.Player2.ManaMax } }

        | PlayerPlayedCard card ->
            match state.Current with
            | Some Player1 -> { state with Player1 = { state.Player1 with Mana = state.Player1.Mana - card } }
            | Some Player2 -> { state with Player2 = { state.Player2 with Mana = state.Player2.Mana - card } }

        | PlayerEndedTurn player ->
            match player with
            | Player1 -> { state with Current = Some Player2 }
            | Player2 -> { state with Current = Some Player1 }
                
        | _ -> state
   
    events |> List.fold handleEvent {
        Player1 = { Deck = []; Hand = []; Mana = 0; ManaMax = 0; Health = 30 }
        Player2 = { Deck = []; Hand = []; Mana = 0; ManaMax = 0; Health = 30 }
        Current = None
    }

type CommandHandler = {
    handle : Cmd -> Evt list -> Result<Evt list, Error>
}

let private beginGame (cmd: BeginGame) (state: Game) =
    let firstPlayer = cmd.FirstPlayer
    let opponent = match firstPlayer with
                   | Player1 -> Player2
                   | Player2 -> Player1
    
    let deck = match firstPlayer with
               | Player1 -> state.Player2.Deck
               | Player2 -> state.Player1.Deck

    let card = cmd.PickedCard
    if deck |> List.contains card
    then Result.Ok [
             FirstPlayerChosen firstPlayer;
             PlayerPickedACard {
                 Player = opponent
                 Card = card
                 }
             ]
    else Result.Error { Message = "Noooo !!!!" }

let apply (cmd: Cmd) (history: Evt list) : Result<Evt list, Error> =
    
    match cmd with
    | CreateGame create -> 
        let player1Card1::player1Card2::player1Card3::_ = create.DeckPlayer1
        let player2Card1::player2Card2::player2Card3::_ = create.DeckPlayer2
        
        Result.Ok [GameCreated {
                        DeckPlayer1 = create.DeckPlayer1
                        DeckPlayer2 = create.DeckPlayer2
                    };
                   HandInitiated {
                       Player = Player1
                       Card1 = player1Card1
                       Card2 = player1Card2
                       Card3 = player1Card3
                    };
                   HandInitiated {
                       Player = Player2
                       Card1 = player2Card1
                       Card2 = player2Card2
                       Card3 = player2Card3
                    }]

    | BeginGame beginGameCmd -> 
        let state = hydrate history
        state |> beginGame beginGameCmd

    | StartNewTurn -> 
        let state = hydrate history
        let currentPlayer = match state.Current with
                            | Some player -> player
                            | _ -> Player1
        Result.Ok [PlayerGotMana currentPlayer;
                      PlayerGotManaMax currentPlayer;
                      PlayerPickedACard {
                          Player = currentPlayer
                          Card = 0
                        }]
    
    | PlayCard card ->
        let state = hydrate history
        let currentPlayer = match state.Current with
                            | Some Player1 -> state.Player1
                            | _ -> failwith "t'as qu'à implémenter !!"

        if currentPlayer.Hand |> List.contains card |> not
        then Result.Error { Message = "Don't have the card"}
        elif currentPlayer.Mana >= card
        then 
            let opponent = match state.Current with
                            | Some Player1 -> Player2
                            | _ -> failwith "t'as qu'à implémenter !!!"
            Result.Ok [PlayerPlayedCard card; PlayerHealthReduced  {
                Player = opponent
                HealthReduced = card
            }]
        else Result.Error { Message = "Not enough mana" }
    
    | EndTurn -> Result.Ok [PlayerEndedTurn Player1]

let createCommandHandler : CommandHandler = {
    handle = apply
}
