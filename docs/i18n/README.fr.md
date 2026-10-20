# Codex Update Center

[简体中文](../../README.md) · [English](../README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português (Brasil)](README.pt-BR.md)

### Moins de complications pour mettre à jour Codex sous Windows.

**Repérez votre installation, préparez une mise à jour officielle vérifiée et confirmez le résultat réel, dans une seule fenêtre.** Pour éviter de rechercher sans cesse les dossiers d'installation, de retélécharger de gros paquets ou de comprendre pourquoi une mise à jour ne fonctionne pas.

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[Télécharger la préversion Windows](https://github.com/son-gladyso/codex-update-center/releases) · [Signaler un problème](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · ZIP autonome avec .NET inclus · MIT · Projet communautaire indépendant, sans affiliation à OpenAI

![Sélection de l'installation, vérification du candidat, coût réseau et confirmation de l'installation](../images/update-center.png)

*Application réelle avec des données d'exemple synthétiques. L'interface est actuellement en chinois ; cette image ne prouve pas la mise à jour d'une installation réelle d'un utilisateur.*

## Les difficultés de mise à jour dans une seule fenêtre

| Difficulté | Réponse de l'outil |
| --- | --- |
| Quelle installation mettre à jour, application de bureau ou CLI ? | Détecte les installations MSIX officielles de l'utilisateur courant et les préfixes npm standards de la CLI. Les installations restent distinctes ; seule la cible sélectionnée est mise à jour. |
| Un téléchargement interrompu doit tout recommencer | Conserve les points de reprise. Les gros composants natifs de la CLI utilisent des plages reprenables de 1 MiB ; les paquets officiels en cache sont revérifiés avant réutilisation. |
| Le paquet est prêt, mais je travaille encore | Sépare préparation et installation. L'installation demande confirmation et vérifie les processus en cours ; Codex n'est jamais arrêté de force. |
| Le résultat annonce un succès, mais la CLI ne démarre pas | Redétecte la version installée et exécute la commande de version de la CLI ; le succès exige des résultats concordants. |
| Je ne veux pas épuiser mon forfait mobile | Bloque par défaut les gros téléchargements sur les réseaux facturés à l'usage ou de coût inconnu. Le téléchargement courant peut être explicitement autorisé ; le coût est revérifié avant l'installation npm. |
| Le disque C manque d'espace | Vérifie le budget et les marges de sécurité. Préfère C uniquement si ses réserves suffisent ; sinon examine les disques fixes internes admissibles et affiche l'emplacement du cache. |

**Les signatures et empreintes du contenu des paquets officiels sont vérifiées avant installation.** Aucune API key n'est nécessaire. L'outil ne lit ni jetons de compte ni bases de conversations, ne collecte pas de télémétrie et ne remplace pas la configuration de votre lanceur.

## Démarrer en quatre étapes

1. Téléchargez `CodexUpdateCenter-*-win-x64.zip` depuis [Releases](https://github.com/son-gladyso/codex-update-center/releases) et vérifiez `SHA256SUMS.txt`.
2. Extrayez l'archive dans un dossier accessible en écriture et lancez `CodexUpdateCenter.exe`. Le runtime .NET est inclus.
3. Sélectionnez une installation et préparez sa mise à jour. Lorsqu'une seule installation prise en charge est présente, elle est sélectionnée et préparée automatiquement par défaut ; cette option de démarrage peut être désactivée.
4. Attendez la vérification du candidat, sauvegardez votre travail, fermez normalement le Codex sélectionné, puis confirmez l'installation. Vérifiez ensuite la version redétectée.

Les dossiers npm personnalisés peuvent être ajoutés manuellement s'ils ne sont pas détectés. Si Codex n'est pas encore installé, la fenêtre fournit les instructions officielles d'installation.

> Il s'agit d'une **préversion communautaire**, sans signature Authenticode commerciale. Ne désactivez pas la sécurité de Windows pour l'exécuter. La matrice de déploiement sur Windows propre, les essais physiques d'accessibilité et certains cas de proxy et de réseau restent à vérifier. Consultez le [registre de validation](../VALIDATION.md).

## Installations prises en charge

| Installation | Prise en charge |
| --- | --- |
| Application de bureau `OpenAI.Codex` x64 de l'utilisateur courant, avec signature officielle du Store | Détection et vérification du MSIX officiel, puis mise à jour via les services de déploiement Windows. Le déploiement Windows réel attend encore une recette complète. |
| Installation globale stable de `@openai/codex` via Node.js/npm standard | Détection et vérification du paquet principal et des composants natifs Windows, puis mise à jour après confirmation. Une mise à jour réelle a été testée dans un dossier isolé. |
| Plusieurs dossiers npm globaux, ou coexistence du bureau et de la CLI | Affichage séparé, mise à jour d'une cible à la fois et ajout manuel de préfixes npm. |
| Copies portables, lanceurs multi-instances personnalisés, WSL, ARM64 et structures liées de gestionnaires de versions Node | Pas de prise en charge automatique ; utilisez l'outil d'installation d'origine. |

L'application de bureau et la CLI sont maintenues séparément. L'outil n'insère pas une CLI autonome dans l'application. Placer le cache sur un autre disque ne change pas l'emplacement du bureau géré par Windows.

## Des résultats explicites en cas de problème

- Les points de reprise sont conservés après une pause ou la fermeture de la fenêtre. Une pause demandée par l'utilisateur ne reprend pas automatiquement.
- Une installation échouée, interrompue ou dont la version n'est pas confirmée demande une nouvelle inspection. **Le simple retour de l'installateur ne constitue pas un succès.**
- L'installation npm n'est pas atomique ; un échec peut nécessiter une réparation npm. Aucun retour arrière automatique n'est garanti.
- L'outil ne force pas l'arrêt de Codex, n'installe pas un candidat plus ancien, ne migre ni comptes ni conversations et ne lance aucun service permanent en arrière-plan.
- Il utilise le proxy effectif du système en conservant la validation TLS. Il ne commande pas les nœuds VPN et ne promet ni vitesse maximale ni téléchargements rapides sur tous les réseaux. Certaines requêtes d'index npm peuvent échouer derrière des proxies ; le cache et les traces d'échec sont conservés.

Voir [assistance et dépannage](../SUPPORT.md). L'état est stocké dans `%LOCALAPPDATA%\CodexUpdateCenter\Public` ; les emplacements de cache figurent dans la vue des tâches. Supprimer cet outil ne désinstalle pas Codex. Vérifiez les fichiers d'état et de cache avant de les nettoyer.

## Périmètre des vérifications

- **81 tests du programme** couvrent les changements de cible, signatures invalides, reprises de téléchargement, verrous de concurrence, pauses et contrôles de coût réseau à l'installation.
- **10 contrôles des outils de publication** couvrent les paquets altérés, fichiers cachés oubliés, chemins hors périmètre et limites de confidentialité du code source.
- Mise à jour réelle de **CLI 0.161.0 → 0.162.1** dans un nouveau dossier isolé, avec exécution de la nouvelle CLI pour vérifier sa version. Les manifestes des installations existantes sont restés inchangés.
- Le paquet officiel de bureau a passé la vérification de signature Windows et de toutes les empreintes de blocs. La compilation indépendante depuis le paquet source et l'extraction puis le lancement du paquet exécutable ont aussi été vérifiés.

Le test de mise à jour réelle réutilisait des paquets officiels revérifiés et des métadonnées npm en cache. **Ce n'est pas une validation réseau à cache vide.** Vérifier signatures et empreintes ne prouve pas un déploiement Windows réel. Les dernières exécutions sont dans [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions), les détails et points en attente dans [VALIDATION](../VALIDATION.md).

## Commencer par inspecter, sans installer

Pour déterminer si l'outil convient, extrayez-le et examinez les installations détectées et leurs versions actuelles. Vous n'avez pas à confirmer immédiatement une installation. La préparation peut accéder au réseau et télécharger des fichiers ; la préparation automatique d'une unique installation compatible peut être désactivée.

- Exécution : Windows x64 et dossier d'extraction accessible en écriture. .NET inclus, aucune API key requise.
- Vérification et préparation : accès aux sources officielles, selon le proxy, les contrôles de coût réseau et les réserves disque.
- Installation : sauvegardez, quittez normalement le Codex sélectionné, revérifiez la cible et confirmez uniquement lorsque vous êtes prêt.

**Premier résultat satisfaisant :** vous identifiez le type, l'emplacement et la version de la cible, et comprenez son état de vérification ou de blocage. Une mise à jour n'est réussie qu'après les contrôles post-installation requis.

## Questions fréquentes

**L'application de bureau et la CLI forment-elles une seule installation ?** Non. Elles sont détectées et mises à jour séparément ; un résultat de version CLI ne prouve pas le déploiement du bureau.

**Pourquoi ma copie portable ou mon montage multi-instances personnalisé n'apparaît-il pas ?** Ces structures sortent du périmètre automatique. Utilisez leur outil d'origine, sans les faire passer pour des installations compatibles.

**Pourquoi bloquer l'installation après téléchargement ?** Il reste à vérifier le candidat, confirmer la cible et respecter les contrôles applicables de processus actifs et de coût réseau. Téléchargement terminé ne signifie pas installation terminée.

**Pourquoi bloquer un réseau dont le coût est inconnu ?** C'est la protection de coût par défaut. Vérifiez votre réseau et autorisez explicitement le téléchargement courant si nécessaire ; n'affaiblissez ni TLS ni les contrôles de signature.

**Un échec restaure-t-il automatiquement l'ancienne version ?** Aucun retour arrière n'est garanti. npm n'est pas atomique. Examinez le résultat et le [guide d'assistance](../SUPPORT.md), plutôt que de confirmer en boucle.

**Que faire si Windows avertit au sujet de l'outil ?** La préversion n'a pas de signature Authenticode commerciale. Vérifiez sa provenance et les sommes de contrôle, ne désactivez pas la sécurité et ne l'exécutez pas si vous ne pouvez pas établir sa fiabilité.

### Trouver les responsabilités avant de modifier

| Domaine | Point d'entrée | À expliquer dans la PR |
| --- | --- | --- |
| Détection et sélection de cible | `src/UpdateCenter.Core/Discovery.cs` | Prévention des mauvaises cibles et traitement des structures non prises en charge. |
| Réseau officiel et vérification | `OfficialNetwork.cs`, `Verification.cs` | Cas observables d'échec, reprise et rejet, sans affaiblir les contrôles de confiance. |
| Parcours de mise à jour et persistance | `UpdateService.cs`, `Storage.cs` | Pause, interruption, état après redémarrage et vérification après installation. |
| Interface et messages | `src/UpdateCenter.App/MainWindow.xaml` et son code-behind | Couverture de régression des actions répétées, transitions et messages d'erreur. |
| Compilation et publication | `scripts/`, `docs/RELEASING.md` | Résultats réels d'audit, de tests et de contrôle des manifestes. |

Les noms courts des fichiers Core sont dans `src/UpdateCenter.Core/`. Voir l'[architecture](../ARCHITECTURE.md) ; une réponse de succès simulée ne démontre pas un déploiement Windows natif.

## Développement et contributions

Compilez sur Windows x64 avec PowerShell 7 et le SDK indiqué dans `global.json` :

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

Les contrôles de publication auditent les sources, restaurent les dépendances verrouillées, exécutent les tests, produisent des paquets autonomes et vérifient les manifestes fichier par fichier. Le dossier de sortie doit être nouveau ; les tests ordinaires n'installent ni ne modifient Codex. La CI Windows et la recette native isolée distinguent les différents périmètres de preuve.

[Architecture](../ARCHITECTURE.md) · [Contributions](../../CONTRIBUTING.md) · [Publication](../RELEASING.md) · [Historique](../../CHANGELOG.md) · [Sécurité](../../SECURITY.md)

Les retours utiles précisent le type d'installation, la version Windows, l'étape bloquante et le résultat attendu. Utilisez les [modèles de problème ou suggestion](https://github.com/son-gladyso/codex-update-center/issues/new/choose). Ne joignez jamais d'identifiants secrets, conversations ou journaux bruts de compte. Les petites corrections reproductibles et les messages d'échec plus clairs sont bienvenus.

### Une première contribution

- **Documentation et messages :** transformer un échec observé en étapes reproductibles, ou clarifier types d'installation, emplacements du cache et erreurs.
- **Tests de régression :** couvrir changements de cible, pause/reprise ou échec de vérification de paquet, en décrivant le résultat observable.
- **Validation Windows :** noter précisément système, type d'installation, versions et périmètre ; distinguer tests synthétiques, opérations natives et vérification en ligne.

Lisez le [guide de contribution](../../CONTRIBUTING.md) et le [registre de validation](../VALIDATION.md), puis choisissez un problème ciblé. Les tests qui installent réellement doivent utiliser un environnement isolé, pas votre installation de travail quotidienne par défaut. Discutez des règles de détection, du mécanisme de mise à jour et des échecs avant d'annoncer un nouveau type pris en charge.

La page montre une capture de l'application réelle avec des exemples, pas un enregistrement continu de la préparation à la fin de l'installation. Les guides reproductibles peuvent être améliorés, mais des états de démonstration ne doivent pas être présentés comme des mises à jour réelles.

MIT couvre le code de cet outil. Les archives distribuées ne contiennent pas Codex ; ses paquets sont téléchargés depuis les sources officielles à l'exécution et conservent leurs propres licences et conditions. Voir [NOTICE](../../NOTICE.md).
