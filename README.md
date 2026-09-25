# TransitRealtime

Service de transit temps réel pour microservices : une app source POST un payload sur le webhook d'un
service, le serveur le redistribue instantanément à tous les clients WebSocket déjà abonnés à ce service.

## Stack

- ASP.NET Core 10 (Razor Pages), hébergement IIS via ANCM in-process
- SignalR (1 hub, un groupe par service) pour le fan-out temps réel
- SQLite + EF Core pour les services, clés API et logs d'audit
- ASP.NET Core Identity (local, email/mot de passe) pour l'interface admin

## Lancer en local

```bash
cd src/TransitRealtime.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmin:Password" "UnMotDePasseSolide123!"
dotnet run
```

Au premier démarrage, le rôle `Admin` et le compte défini dans `SeedAdmin` sont créés automatiquement
(voir `Data/SeedData.cs`). Les migrations EF Core sont appliquées automatiquement au démarrage.

Une fois connecté, va sur `/Services` pour créer un service : tu obtiens une URL de webhook, l'URL du
hub SignalR, et une clé API affichée **une seule fois**.

## Flux

Deux modes de diffusion, au choix par service :

**1. Diffusion large** (tous les abonnés du service reçoivent tout) :
1. Admin crée un service → URL webhook + clé API générées.
2. Un client ouvre une connexion SignalR vers `/hub` et appelle `JoinService("<serviceId>")`.
3. Une app source POST sur `/webhook/{serviceId}` avec le header `X-Api-Key: <clé>`.
4. Le serveur valide la clé et diffuse le payload (événement `ReceivePayload`) à tous les clients du
   groupe `serviceId`.

**2. Ciblé par destinataire** (recommandé dès que les payloads sont sensibles / propres à un utilisateur) :
1. Admin crée un service → en plus de la clé API, un **secret d'abonné** est généré (affiché une seule
   fois, régénérable depuis `/Services/Details`).
2. Le backend source signe un JWT HS256 avec ce secret : claims `sub=<recipientKey>`, `svc=<serviceId>`,
   courte durée de vie (ex: 60s), et le transmet au client concerné (ex: via un endpoint d'auth existant
   côté app source).
3. Le client appelle `JoinChannel("<serviceId>", "<recipientKey>", "<token>")` sur le hub — rejeté si le
   token est invalide, expiré, ou si `sub`/`svc` ne correspondent pas à ce qui est demandé.
4. L'app source POST sur `/webhook/{serviceId}/{recipientKey}` (même header `X-Api-Key`) → seul le
   sous-groupe `serviceId::recipientKey` reçoit le payload. Les autres abonnés du même service ne voient
   rien passer.

## Créer d'autres comptes admin

Une fois connecté en tant qu'Admin, le lien "+ Compte admin" (en haut à droite) permet de créer d'autres
comptes admin via `/Admin/Users/Create`. L'auto-inscription publique est désactivée.

## Sécurité déjà en place

- Clés API générées aléatoirement (256 bits), stockées hashées (jamais en clair) via `PasswordHasher`.
- Rate limiting sur `/webhook/{serviceId}` (60 req/min par défaut, `Program.cs`).
- Taille de requête webhook plafonnée à 256 KB (`Program.cs`, `MaxRequestBodySize`).
- Toutes les pages `/Services` et `/Admin` exigent le rôle `Admin`.
- Logs d'audit (webhook reçu, échec d'auth, connexions/déconnexions clients, gestion des clés).

## Limitations connues (MVP)

- Le mode "diffusion large" (`JoinService`) ne demande que le `serviceId`, sans authentification — à
  réserver aux services dont les payloads ne sont pas sensibles. Pour tout le reste, utiliser le mode
  ciblé (`JoinChannel` + JWT signé par le secret d'abonné).
- Pas de rôles différenciés (Admin uniquement pour l'instant).
- Un seul serveur / pas de backplane SignalR — suffisant tant qu'on reste sur une seule instance IIS.

## Déploiement sur Windows Server / IIS

1. Installer le **.NET 10 Hosting Bundle** sur le serveur.
2. Activer la fonctionnalité Windows *IIS → World Wide Web Services → Application Development Features →
   WebSocket Protocol*.
3. Importer le certificat wildcard `*.du-simandou.gov.gn` (.pfx) dans le magasin **Machine → Personal**.
4. `dotnet publish -c Release -o publish` puis copier le contenu de `publish/` sur le serveur.
5. Créer un site IIS pointant vers ce dossier, pool d'applications en **No Managed Code**, avec :
   - Binding `http` :80 sur `transit-realtime.du-simandou.gov.gn`
   - Binding `https` :443, même hostname, certificat SNI = le wildcard importé
   - `ws://` et `wss://` n'ont pas de binding séparé : ils passent par ces mêmes ports via l'upgrade
     HTTP standard, dès que le module WebSocket Protocol est actif.
6. Définir `SeedAdmin:Email` / `SeedAdmin:Password` en variables d'environnement IIS (ou
   `appsettings.Production.json`, hors dépôt git) pour le premier compte admin en production.
7. Lister dans `Cors:AllowedOrigins` les origines des apps dont le navigateur se connecte au hub
   (ex: `https://ledelivreur.du-simandou.gov.gn`) : sans cela, `/hub/negotiate` est bloqué par le
   navigateur. `Webhook:PermitPerMinute` (défaut 60, fenêtre globale à tous les appelants) doit
   couvrir les diffusions massives : une notification à N destinataires coûte N appels.
8. Vérifier `https://transit-realtime.du-simandou.gov.gn/webhook/{id}` et
   `wss://transit-realtime.du-simandou.gov.gn/hub`.

Le fichier SQLite (`app.db`) est créé dans le dossier de l'application — s'assurer que le compte du pool
d'applications IIS a les droits d'écriture dessus.
