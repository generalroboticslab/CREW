# Nakama Server

This folder sets up the Nakama server runtime for Dojo.\
Nakama matches will always be launched in player-relayed mode.

Registered RPCs:
* `RPCJoinOrNewMatch`\
  Join or request a new match by `Client` instances.
* `RPCRemoveUserAccount`\
  Remove and clean up user account on Nakama.
* `RPCCreateMatch`\
  Create match-related storage by `Server` instances.
* `RPCUpdateMatch`\
  Update match-related storage by `Server` instances.
* `RPCCleanMatch`\
  Clean up match-related storage by `Server` instances.
* `RPCQueryMatch`\
  Query match info from storage by `Client` instances.
* `RPCQueryMatches`\
  Query all active matches from storage by `Client` instances.

------

### Deployment

[Golang](https://go.dev/) and [Docker](https://www.docker.com/) (with compose) are required to start Nakama server. After installation, run:
```cmd
./run.bat
```
or
```bash
./run.sh
```
