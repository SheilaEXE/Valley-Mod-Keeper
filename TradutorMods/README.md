# Valley Modkeeper

Aplicativo visual para localizar traduções em vários idiomas numa biblioteca pessoal, aplicá-las aos mods instalados e preservar configurações antes de atualizar mods.

A interface inicia em inglês para novos usuários e pode ser alterada para português do Brasil pelo seletor no topo da janela. A escolha é salva em `%LOCALAPPDATA%\TradutorModsStardew\language.json`. O programa reconhece arquivos de idioma dentro de `i18n` (por exemplo, `pt.json`, `pt-BR.json`, `es.json`), pastas inteiras de idioma como `i18n/pt/`, pastas de idioma em `assets/Dialogues` e mapas `content.<idioma>.json` que já seguem o formato próprio do programa. O seletor **Translation language** mostra e aplica um idioma por vez. As pastas `de`, `cs`, `pt-BR` etc. incluídas nas compilações Windows são recursos do .NET/Windows Forms, não traduções próprias deste aplicativo.

O título usa [Pixelify Sans](https://github.com/google/fonts/tree/main/ofl/pixelifysans), distribuída sob a SIL Open Font License 1.1 (veja `Assets/PixelifySans-OFL.txt`).

## Regras de segurança

- Nunca substitui uma tradução já existente no mod para o mesmo idioma (`pt` e `pt-BR` continuam protegidos entre si).
- **Find new mods / Procurar novos mods** guarda referências `i18n/default.json` ou `i18n/default/` e `content.json`, e copia para a biblioteca arquivos ou pastas `i18n` do idioma escolhido que ainda não tenham cópia. Nunca substitui automaticamente uma cópia existente.
- Após **Analyze / Analisar**, cada mod instalado com pasta `i18n` mas sem arquivo ou pasta do idioma selecionado aparece como **Missing translation file / Arquivo de tradução ausente**, exceto quando já há uma tradução correspondente na biblioteca. Duplo clique nessa linha permite escolher um arquivo JSON ou a pasta inteira do idioma; é possível navegar a outro lugar. A importação exige confirmação, e arquivos existentes na biblioteca são protegidos por backup antes de uma substituição manual.
- A importação manual aceita arquivos de tradução `i18n` em JSON e pastas completas de idioma; mapas especiais `content.<idioma>.json` já existentes na biblioteca são analisados separadamente.
- A janela **Settings backup / Backup de configurações** contém a busca por novos `config.json`, a exportação e a restauração. Buscar apenas atualiza a lista; exportar é uma ação separada.
- Só seleciona automaticamente correspondências consideradas seguras.
- Pacotes com vários componentes aparecem agrupados; marcar o grupo seleciona somente os componentes seguros.
- Uma correspondência incerta precisa ser confirmada com duplo clique.
- As escolhas manuais são lembradas pelo `UniqueID` do mod.
- Cada aplicação gera relatório e pode ser desfeita durante a sessão.

## Uso

1. Escolha a biblioteca de traduções e uma pasta `Mods`.
2. Escolha o idioma das traduções no topo e clique em **Analyze / Analisar**.
3. Use **Find new mods / Procurar novos mods** para guardar novas referências e traduções existentes nos mods.
4. Revise as linhas amarelas e as linhas de arquivo ausente com duplo clique.
5. Clique em **Apply selected / Aplicar selecionadas** para as traduções prontas.

Faça o primeiro teste com uma cópia da pasta `Mods`.
