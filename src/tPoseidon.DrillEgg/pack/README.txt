THE DRILL EGG
=============

This is the Drill Egg gacha from Trickster, rebuilt so it runs on its own.
You pick one of four dig sites, choose the Normal Drill or the Deep Drill,
drill where you think the treasure is, and a prize card shows what you dug
up. It runs in any web browser. You do not need a game server, a database
or anything else installed.

It works on Windows, Linux and macOS. Each zip is for one system:

    ...-win-x64.zip      Windows
    ...-linux-x64.zip    Linux (most PCs and servers)
    ...-osx-arm64.zip    macOS on Apple chips (M1 and newer)
    ...-osx-x64.zip      macOS on Intel


QUICK START
-----------

1. Unzip the folder anywhere you like.
2. Put your copy of myshop.swf in the folder, next to start.cmd and
   start.sh (see "The game art" below).
3. Start it:
     Windows: double-click start.cmd
     Linux or macOS: open a terminal in the folder and type  sh start.sh
4. Your browser opens the game. If it does not, open
   http://localhost:8080/ yourself.

To stop it, close the window or press Ctrl+C in it.

If your zip's name ends in "-with-art", the game art is already inside
and you can skip step 2.


THE GAME ART
------------

The pictures, animations and sounds belong to the original game, so they
are not in this pack. They are made from one file of your own copy of the
Drill Egg gacha: myshop.swf.

Copy myshop.swf into this folder. The first time the Drill Egg starts, it
reads that file and makes an "art" folder from it, which takes a few
seconds. After that, myshop.swf is not needed any more, but it does no
harm to keep it.

If the file is not there yet, the browser shows a page that says exactly
where to put it. Copy the file, then press "Check again"; there is no need
to restart.

You can keep myshop.swf somewhere else: change "swf" under "files" in
drill-egg.json to its full path, for example
    "swf": "D:/Games/drill-egg/myshop.swf"
(use forward slashes, or double backslashes like D:\\Games\\myshop.swf).

You can also make the art by hand:
     Windows:          start.cmd extract myshop.swf art
     Linux or macOS:   sh start.sh extract myshop.swf art


THE SETTINGS FILE
-----------------

All settings live in drill-egg.json, a text file you can open with
Notepad, TextEdit or any text editor. Restart the Drill Egg after you
change it. If something in it is wrong, the window tells you which line
and what to fix.

The examples folder holds ready-made settings files. To use one, copy it
over drill-egg.json (keep a copy of your old one).

    standalone.json   the default: one player, free play
    coupons.json      players type a name, everyone starts with 5 coupons
    tposeidon.json    plugs into a tPoseidon game server
    sqlserver.json    plugs into a game server's SQL Server database
    webhook.json      asks a game server's web address instead

Settings at the top:

    "port"         the number at the end of the address (8080 means
                   http://localhost:8080/). Change it if another program
                   already uses 8080.
    "listen"       "localhost" means only this computer can play.
                   "any" lets other computers in, see "Letting other
                   people play".
    "openBrowser"  true opens the browser when it starts. Use false on a
                   server.
    "files"        where myshop.swf, the art folder, the prize pictures
                   and the data file are. Paths are relative to this
                   folder unless you give a full path.


PRIZES AND CHANCES
------------------

The prizes are the list under "prizes". Each prize looks like this:

    { "id": 9, "name": "Crystal Egg", "weight": 2, "rare": true }

    "id"          a number for the prize. On a game server it is the item
                  number the server uses; on your own you can pick any
                  number above 0.
    "name"        what the prize card shows.
    "weight"      how likely it is. The chance is its weight divided by
                  all the weights added up. With weights 30, 20 and 50,
                  the first prize comes up 30 times in every 100 draws.
    "count"       optional: how many of the item you win (default 1).
    "rare"        optional: true makes the Deep Drill find it more often.
    "deepWeight"  optional: the exact weight in the Deep Drill, if you
                  want to set it yourself.
    "picture"     optional: the picture's file name in the pictures
                  folder (see below).

The Normal Drill costs 1 coupon and the Deep Drill costs 2. The Deep
Drill uses the same prizes, but every "rare" prize's weight is multiplied
by "rareBoost" under "deepDrill" (3 by default). So a rare prize with
weight 2 has weight 6 in the Deep Drill. A prize with "deepWeight" uses
that number in the Deep Drill instead.

Each of the four dig sites can have its own prizes. The sites are
numbered 61 (top left), 62 (top right), 63 (bottom left) and 64 (bottom
right). Put a list under "areas" for a site; sites without one use the
main "prizes" list:

    "areas": {
      "64": { "prizes": [
        { "id": 4, "name": "Lucky Clover", "weight": 40 },
        { "id": 10, "name": "Legendary Drill Egg", "weight": 2, "deepWeight": 10 }
      ] }
    }

To check your chances, run
     Windows:          start.cmd prizes
     Linux or macOS:   sh start.sh prizes
It lists every dig site and drill with each prize's chance in percent.


PRIZE PICTURES
--------------

The original game kept its prize pictures elsewhere, so they are not in
myshop.swf. Put your own pictures in the "pictures" folder:

    pictures/9.png               named after the prize id, or
    pictures/Crystal Egg.png     named after the prize name, or
    any name you like, set with "picture" on the prize.

PNG, GIF, JPG and WEBP all work. The card shows the picture at 130 x 150
pixels, so pictures of about that shape look best. A prize without a
picture gets a neat egg card instead. You can add pictures while the Drill
Egg runs.


COUPONS
-------

Free play is the default: everyone can drill as often as they like, and
the coupon counter just shows 99.

For coupons, set
    "coupons": { "connector": "local", "startingCoupons": 10 }
Each new player starts with "startingCoupons" coupons. When they run
out, the game shows its "no coupons left" screen.

To give a player more coupons (the Drill Egg can stay running):
     Windows:          start.cmd coupons Alice 10
     Linux or macOS:   sh start.sh coupons Alice 10
A negative number takes coupons away. With the default single player,
the player's name is "Player".


PLAYERS
-------

    "players": { "connector": "local", "mode": "single", "name": "Player" }
        Nobody signs in; everyone who opens the page is that one player.

    "players": { "connector": "local", "mode": "name" }
        Each player types a name first. There is no password, so this is
        for friends you trust.

For real accounts with passwords, connect to a game server (below).

To see the latest draws:
     Windows:          start.cmd history          (or: history Alice)
     Linux or macOS:   sh start.sh history


LETTING OTHER PEOPLE PLAY
-------------------------

1. In drill-egg.json set "listen": "any" and "openBrowser": false.
2. Allow the port (8080, or the one you chose) in your firewall. At home,
   also forward that port on your router to this computer.
3. Others open http://<your address>:8080/ in their browser.

Please keep it safe:
- With "mode": "name" anybody can play under any name. Use coupons only
  with real sign-in (a game server connector) if they matter.
- On the internet, put the Drill Egg behind HTTPS: run a web server such
  as Caddy or nginx in front of it with a certificate, and keep the Drill
  Egg itself on "listen": "localhost". Passwords should never travel
  over plain http.
- drill-egg.json can hold database passwords and secrets. Do not share
  it, and make sure only you can read it on a shared machine (on Linux:
  chmod 600 drill-egg.json).
- Give database logins only the rights the queries need.
- After ten wrong sign-ins, an address has to wait ten minutes.


CONNECTING TO A GAME SERVER
---------------------------

The Drill Egg has three parts you can connect separately:

    "players"    who is playing (sign-in)
    "coupons"    where coupons are counted and spent
    "delivery"   where a won prize goes

Each one has a "connector":

    local       kept by the Drill Egg itself (the default)
    free        coupons only: free play
    sql         runs your own queries on the game's database
    http        asks web addresses on the game server
    tposeidon   uses a tPoseidon game server directly

You can mix them, for example players and coupons from the game's
database and prizes sent to a web address.

Every draw, whatever the connectors, is also written to drill-egg.db
in this folder, the Drill Egg's own data file.

-- sql --

List the database under "databases" with a name you choose:

    "databases": {
      "game": { "provider": "sqlserver", "connection": "Server=...;Database=...;User Id=...;Password=..." }
    }

"provider" is one of:
    sqlserver   Microsoft SQL Server
    mysql       MySQL or MariaDB
    postgres    PostgreSQL
    sqlite      an SQLite file

"connection" is the usual connection string for that database.

Then point "players", "coupons" and "delivery" at it with
"database": "game" and write the queries. Values go into the queries as
@names. They are always passed as proper query parameters, never pasted
into the text, so a player cannot break a query with odd input.

  players (sql)
    "signIn": "password"   the page asks for account and password
    "signIn": "token"      players arrive with a link, see "Sign-in links"
    "query":   gets @account and @password (or @token). It must return
               one row for a good sign-in and no row for a bad one. The
               row may have these columns:
                 id          the account's number (used as @accountId)
                 account     the account's name (else the typed name;
                             token sign-in needs this column)
                 character   a character name (used as @character)
               Check the password in the query in the way the game
               stores it.

  coupons (sql)
    "count":   gets @account, @accountId and @character, and returns
               the number of coupons.
    "spend":   also gets @cost (1 or 2) and @drawId. It must take the
               coupons only if the player has enough. It counts as spent
               when it changes at least one row, or when it returns a
               number above 0 (for a stored procedure that answers
               SELECT 1 or SELECT 0). The UPDATE ... WHERE coupons >= @cost
               form in sqlserver.json does this safely.

  delivery (sql)
    "deliver": puts the prize where the game expects it, usually an
               INSERT into a gift box or cash item table, or EXEC of a
               procedure. It gets @account, @accountId, @character,
               @itemId, @itemName, @count, @itemClass, @useHours,
               @itemFlag, @drawId, @area (61-64) and @deep (true for the
               Deep Drill). Raise an error to say it failed.

When coupons and delivery use the same database, the spend and the
delivery run in one transaction: either both happen or neither does, and
a failed delivery never eats a coupon. When they are in different
places, the coupons are spent first and the prize is kept as "owed"
until it is delivered; the Drill Egg tries again every "retrySeconds"
under "delivery" (60 by default). @drawId is the same on every try, so
the game side can skip a draw it already has.

The tables and columns in sqlserver.json are placeholders: everything in
CAPITALS starting with YOUR_ must be changed to your server's own names.
For MySQL use backticks or plain names and the MySQL password functions;
if a query uses MySQL @variables of its own, add
AllowUserVariables=True to the connection string.

-- http --

The Drill Egg sends a POST with a JSON body and reads a JSON answer.
With "secret" set, every request carries it in the header
X-Gacha-Secret (or the header you name in "secretHeader"), so the game
server can check the request came from the Drill Egg. Use https:// on
the internet. "timeoutSeconds" sets how long to wait (10 by default).

  players (http), "url":
    sends     {"account": "...", "password": "..."}  or  {"token": "..."}
    answer    200 with {"account": "Alice", "id": 12, "character": "..."}
              (id and character are optional), or 401, 403 or 404 for a
              bad sign-in.

  coupons (http), "countUrl" and "spendUrl":
    count     sends {"account", "accountId", "character"}
              answers {"coupons": 7}
    spend     also sends "cost" and "drawId"
              answers {"spent": true, "coupons": 5}
              or {"spent": false, "coupons": 1} when there are too few.
              The game server takes the coupons and answers in one step.

  delivery (http), "url":
    sends     {"drawId", "account", "accountId", "character", "itemId",
               "itemName", "count", "itemClass", "useHours", "itemFlag",
               "area", "deep"}
    answer    any 2xx means delivered; anything else is retried later.

-- tposeidon --

Uses the accounts, coupons and item box of a tPoseidon server, in the
same way its own web shop does. Set
    "tposeidon": { "connection": "Host=...;Port=...;Database=...;Username=...;Password=...", "schema": "public" }
with the server's PostgreSQL connection. With tposeidon coupons, the
prizes and chances come from the server's gacha_prize table, so players,
coupons and delivery must all be "tposeidon"; the "prizes" list then only
gives the names and pictures. tposeidon delivery on its own also works
with sql or http sign-in that returns the account's id.

-- Sign-in links --

With "signIn": "token", players open
    http://<the Drill Egg's address>/?token=<their token>
and are signed in at once. The game server makes the token and checks
it in the players query or web address.


FILES IN THIS FOLDER
--------------------

    start.cmd, start.sh   start the Drill Egg
    drill-egg.json        your settings
    examples/             ready-made settings
    pictures/             your prize pictures
    app/                  the program itself
    licenses/             licences of the parts it is built with
    LICENSE.txt           the licence of the Drill Egg
    drill-egg.db          made on first start: players, coupons, draws
    art/                  made from myshop.swf on first start


TROUBLESHOOTING
---------------

The window says the port is in use
    Another program uses that port. Change "port" in drill-egg.json.

macOS says the program cannot be opened
    Start it with  sh start.sh  from a terminal; the script clears the
    download flag that macOS puts on unzipped programs.

The page says the art could not be made
    The file may not be the game's myshop.swf, or it is damaged. Copy it
    again from your original files.

A prize was won but did not arrive
    Run the history command. A draw marked "owed" is still waiting and is
    tried again every minute; the window shows why it failed.


LICENCE
-------

The Drill Egg is part of tPoseidon and has the same terms: the tPoseidon
Beta License in LICENSE.txt. In short, you can run it for yourself, your
friends or a community for free; read the file for the details.

The Drill Egg is an independent fan project. It has no connection with
the owners of Trickster. The game, its art, sounds, names and trademarks
belong to their owners and are not covered by this licence. The parts the
program is built with are listed, with their licences, in the licenses
folder.
