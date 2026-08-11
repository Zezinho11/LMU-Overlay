# Auditoria técnica e de produto — 2026-08-05

## Objetivo

Este documento compara o estado de `0.4.0-preview.5` com:

- os headers oficiais instalados por Le Mans Ultimate;
- a implementação e os testes atuais do repositório;
- documentação oficial de Windows, DirectX, SteamVR, OpenXR, GitHub e FIA;
- literatura revisada por pares sobre estratégia de corrida.

O objetivo não é aumentar o escopo sem controle. É identificar o que ainda
impede o overlay de ser confiável, distribuível e extensível dentro das decisões
já estabelecidas: processo externo, leitura oficial e somente leitura, desktop
de baixa latência, SteamVR e widgets de endurance.

## Resumo executivo

O produto já possui a arquitetura correta: aquisição separada, snapshots
imutáveis, dashboard e torres nativas, WPF como editor/fallback e host SteamVR
independente. A próxima versão não deve priorizar novos widgets.

As cinco maiores lacunas são:

1. **Compatibilidade verificável com atualizações do LMU.** Existe uma única
   versão derivada do layout e uma única gravação anonimizada. O endpoint local
   usado para `optimal` não possui contrato versionado no projeto.
2. **Aquisição orientada pelos eventos oficiais.** O jogo fornece
   `LMU_Data_Event` e contadores separados de scoring/telemetria, mas o runtime
   ainda acorda a cada 4 ms. Isso faz leituras duplicadas e não mede a latência
   produtor→overlay.
3. **Recuperação do renderer.** Um erro de `Present` encerra a thread nativa e
   deixa apenas o fallback. Falta recriar device, swap chain e recursos após
   `DXGI_ERROR_DEVICE_REMOVED`/`RESET`, além de completar Per-Monitor DPI v2.
4. **Validação de corrida longa.** CI usa soak de cinco segundos. Falta replay de
   sessões reais, matriz multiclass e gates de uma a seis horas para memória,
   cadence, device loss e transições.
5. **Estratégia 2.0.** O planejador atual enumera poucas divisões balanceadas e
   usa médias/tendências simples. É um bom estimador determinístico, não um
   otimizador robusto de stint, tráfego, clima e incerteza.

## Estado comprovado

### O que está sólido

- O processo não injeta DLL, não instala hook, não lê memória privada do jogo e
  não automatiza input.
- `LMU_Data` é aberto com `MemoryMappedFileRights.Read`.
- A dashboard recebe apenas o bloco do jogador no fast path e publica o estado
  mais recente sem fila acumulada.
- O hot path visual usa Win32 + Direct3D 11 + Direct2D/DirectWrite +
  DirectComposition; WPF permanece editor e fallback.
- Dashboard, Live Standings e Relative têm estados independentes do renderer,
  o que permite reuso em VR.
- Há cerca de 250 verificações automatizadas distribuídas entre Core, Desktop,
  Domain, Shared Memory, Widgets e SteamVR.
- CI compila e executa todos os executáveis de teste e um soak curto.
- Release produz ZIP portátil, SHA-256 e manifesto de atualização.

### Limitações confirmadas no código

| Área | Estado atual | Efeito |
|---|---|---|
| Aquisição | polling fixo de 4 ms | desperta até 250 vezes/s mesmo sem amostra nova |
| Dashboard | fast path somente do carro do jogador | correto para inputs, RPM e pneus |
| Campo completo | parse completo a cada 200 ms | standings, energia dos rivais e dados multiclass ficam limitados a 5 Hz |
| Renderer dashboard | espera de 8 ms + `Present(1)` | baixa latência, mas sem pacing pelo waitable object |
| Renderer timing | espera de 16 ms + `Present(1)` | pode apresentar repetidamente o mesmo scoring |
| Erro DirectX | exceção encerra a thread | fallback permanente até reiniciar o app |
| DPI nativo | contexto não declarado; Direct2D fixado em 96 DPI | risco de blur/escala incorreta em monitores mistos |
| Optimal | HTTP local a cada 1 s, timeout de 750 ms | dependência de endpoint interno sem schema pinado |
| Relative | distância circular convertida pelo pace de referência | aproximação; especialmente sensível a pit, baixa velocidade e start/finish |
| Pit dos rivais | `mInPits` oficial | o próprio header alerta que pode ser impreciso para carros remotos |
| Número do carro | heurística sobre nome/modelo | pode falhar quando o nome oficial não contém número explícito |
| Estratégia | médias ponderadas, tendência linear e até duas paradas extras | espaço de soluções pequeno e pouca representação de incerteza |
| Release | checksum, mas sem assinatura obrigatória/attestation | usuário não verifica publisher nem origem do build |
| VR | cinco painéis head-locked e configuração JSON | falta interação, transforms completos e testes no headset |

## Descobertas na API oficial do LMU

Os headers em
`Le Mans Ultimate/Support/SharedMemoryInterface/` são a principal fonte
autoritativa disponível para esta integração.

### Eventos devem dirigir a aquisição

`SharedMemoryInterface.hpp` declara:

- `LMU_Data_Event`;
- `SME_UPDATE_SCORING`;
- `SME_UPDATE_TELEMETRY`;
- eventos de sessão, realtime, load/unload e shutdown;
- um contador por tipo no início do mapping.

Recomendação: substituir o timer puro por um **wait híbrido**:

1. aguardar `LMU_Data_Event` com timeout curto cancelável;
2. consultar os contadores;
3. ler somente o bloco cujo contador mudou;
4. manter timeout periódico como proteção contra evento perdido/coalescido;
5. publicar sempre latest-only, nunca uma fila de frames atrasados.

Isso reduz trabalho duplicado e permite medir com precisão:

- instante em que o contador mudou;
- término da cópia coerente;
- publicação do snapshot;
- `Present` do frame correspondente.

### Frequências diferentes devem continuar separadas

Telemetria do jogador e scoring são fluxos distintos. Não há benefício em
reconstruir Live Standings a 240 Hz se o contador de scoring não mudou. O modelo
correto é:

- inputs, marcha, RPM, shift lights: a cada update de telemetria;
- pneus, energia e estado do jogador: a cada update de telemetria;
- posições, voltas, gaps e sessão: a cada update de scoring;
- dados auxiliares HTTP: baixa frequência e fora do hot path.

### Campos que exigem cautela

- `mWear` é fração do desgaste máximo e o comentário oficial avisa que não é
  necessariamente proporcional à perda de grip.
- `mTemperature[3]` é superfície esquerda/centro/direita; o projeto deve manter
  documentada a fórmula que replica a leitura do MFD e testá-la por carro.
- `mInPits` pode ser impreciso para veículos remotos.
- `mID` é slot reutilizável quando alguém sai do multiplayer. Cache por ID deve
  ser invalidado quando identidade, modelo ou sessão mudar.
- `mEstimatedLapTime` pode variar por veículo e setup; não deve ser tratado como
  verdade estável para estratégia.
- `mVirtualEnergy`, gaps de carro e gaps de posição existem no bloco de
  telemetria, mas disponibilidade e semântica precisam ser validadas por classe.

### Endpoint HTTP local

`/rest/watch/standings/history` pode continuar como fonte somente leitura para
o theoretical optimal porque replica a UI oficial, mas deve ser tratado como
**adaptador experimental**:

- schema testado com fixtures JSON de várias versões;
- timeout curto, circuit breaker e cache do último valor válido;
- nenhum efeito na captura principal se o WebUI falhar;
- métrica de idade do dado visível nos diagnósticos;
- feature flag para desligá-lo sem perder dashboard/standings.

## Renderer, latência e responsividade

### Ações prioritárias

1. Migrar os swap chains para flip model explicitamente otimizado e avaliar
   `DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT` com latência máxima 1.
2. Medir, no hardware do usuário, `Present(1)` contra pacing pelo waitable
   object. Tearing não é requisito para um overlay composto e não deve ser
   habilitado sem evidência visual/latência favorável.
3. Tratar `DXGI_ERROR_DEVICE_REMOVED` e `DXGI_ERROR_DEVICE_RESET` em `Present` e
   resize, recriando device, Direct2D, brushes, swap chain e visual.
4. Registrar `ID3D11Device::GetDeviceRemovedReason` nos diagnósticos.
5. Declarar Per-Monitor DPI v2 e tratar `WM_DPICHANGED`, recriando recursos de
   texto/superfície no DPI real do monitor.
6. Adicionar teste manual de dois monitores com 100/125/150/200%, alt-tab,
   sleep/wake, troca de resolução, borderless/fullscreen e driver reset.

A Microsoft recomenda flip model para melhor performance e oferece o frame
latency waitable object para controlar a fila de apresentação. Também orienta
recriar toda a cadeia Direct3D/DXGI após device removed/reset e usar Per-Monitor
v2 para mudanças de DPI por janela:

- [DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)
- [Frame latency waitable object](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_3/nf-dxgi1_3-idxgiswapchain2-getframelatencywaitableobject)
- [Device removed no Direct3D 11](https://learn.microsoft.com/en-us/windows/uwp/gaming/handling-device-lost-scenarios)
- [Per-Monitor DPI v2](https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context)

### Métricas que faltam

Os diagnósticos devem separar:

- taxa de evento do produtor;
- taxa de snapshots novos e duplicados;
- duração da cópia e do parse;
- tempo snapshot→frame publicado;
- tempo frame→`Present` concluído;
- frames descartados por latest-only;
- p50, p95, p99 e máximo por janela de 10 s;
- memória privada, working set, handles GDI/USER, objetos D3D e CPU por thread.

O gate recomendado para dashboard é p99 produtor→publicação, não apenas média
de leitura. Um único máximo isolado deve ser diagnosticado, mas não substituir
percentis.

## Exatidão dos widgets

### Dashboard

- Criar catálogo de carros com limites/formatos verificados: RPM máximo, shift
  lights, TC/ABS disponíveis, unidades e temperatura exibida pelo MFD.
- Salvar um golden snapshot por classe e ao menos um por fabricante GT3.
- Exibir idade/stale somente em modo diagnóstico; congelar ou neutralizar valor
  quando a amostra ultrapassar o limite em vez de aparentar ser atual.
- Validar sectors/optimal em outlap, volta inválida, pit exit, mudança de piloto,
  reinício de sessão e replay.

### Relative

O algoritmo atual ordena corretamente por distância física circular, mas o gap
em segundos usa distância dividida por um pace médio. Isso é uma estimativa.

Melhoria incremental segura:

1. manter a ordem por `mLapDist`;
2. usar gaps oficiais do jogador (`mTimeGapCarAhead/Behind`) quando o row for o
   vizinho oficial correspondente e o valor estiver válido;
3. usar interpolação por duas amostras de scoring para os demais carros;
4. congelar/interpolar com limites em pit lane e velocidades muito baixas;
5. rotular internamente a origem do gap (`official`, `interpolated`, `derived`)
   e testar transições na linha de chegada;
6. nunca trocar distância física por posição de corrida.

### Live Standings

- Cache de veículo deve usar `(session generation, slot ID, identity)` para
  sobreviver à reutilização de slots.
- Fabricante e número precisam de catálogo local data-driven, mantendo a
  heurística apenas como fallback explícito.
- Energia/composto dos rivais devem carregar timestamp próprio. Se o bloco
  daquele carro estiver velho, mostrar indisponível em vez de reutilizar valor.
- Testar qualifying sem volta, volta deletada, troca de piloto, P1 entrando no
  pit, disconnect/reconnect e classes com nomes inesperados.

## Estratégia de corrida 2.0

### O que o modelo atual realmente faz

O código atual:

- aprende até uma janela curta de consumo, Virtual Energy, pace e desgaste;
- aplica média ponderada e uma margem baseada no desvio;
- estima uma tendência linear de pace;
- calcula stint máximo por combustível/configuração;
- distribui os stints futuros de forma balanceada;
- considera somente o mínimo de stints mais até duas paradas extras;
- soma pace base, degradação linear, pit loss e troca de pneus.

Isso produz uma orientação útil, mas “BEST” deve significar melhor entre os
candidatos avaliados, não melhor estratégia global.

### Evolução recomendada

#### Versão 2.1 — determinística e explicável

- Segmentar voltas em `push`, `normal`, `save`, outlap, inlap, pit, chuva e
  inválida; apenas voltas comparáveis treinam pace/consumo.
- Usar mediana/MAD ou estimador robusto antes de média ponderada.
- Modelar combustível e Virtual Energy como restrições independentes.
- Enumerar todo pit window viável, não apenas stints balanceados.
- Modelar pit loss por pista e componentes: entrada, lane, parada e saída.
- Modelar troca de pneus simultânea ou adicional conforme regra/configuração.
- Projetar término de corrida por tempo com incerteza de uma volta.
- Produzir `baseline`, `aggressive` e `safe`, cada um com premissas e margem.
- Exibir intervalo de confiança e motivos de indisponibilidade.

#### Versão 2.2 — cenários

- Recalcular incrementalmente a cada volta e evento relevante.
- Simular clima como cenários, sem afirmar previsão que a API não fornece.
- Incluir tráfego/posição de saída e risco de perder tempo atrás de outro carro.
- Adicionar distribuição de pace, consumo, pit loss e degradação.
- Rodar Monte Carlo com orçamento fixo fora das threads de captura/render.
- Apresentar probabilidade de chegar, de evitar splash e de sair em ar limpo;
  não uma falsa precisão de décimos.

#### Versão 2.3 — competição

- Modelar respostas dos rivais apenas quando houver dados suficientes.
- Priorizar uma solução explicável antes de reinforcement learning.
- Guardar decisões, inputs e resultados para calibração posterior.

A literatura sustenta essa ordem: programação dinâmica encontra combinações de
pit lap/composto, enquanto competição e incerteza alteram a decisão ótima. O
estudo de Aguad e Thraves observou perda relevante ao ignorar a resposta do
oponente:

- [Dynamic programming para pit stops](https://link.springer.com/article/10.1007/s10100-022-00806-4)
- [Dynamic programming e game theory](https://www.sciencedirect.com/science/article/pii/S0377221724005484)

### Regras não devem ser hardcoded

O regulamento FIA WEC muda por temporada/evento e trata energia por stint,
penalidades, pneus e condições específicas. LMU também pode aplicar regras
diferentes da competição real. Portanto:

- telemetria/configuração do evento tem precedência;
- limites manuais devem mostrar sua origem;
- o app não deve inferir uma obrigação real sem campo confirmado;
- presets FIA são referência opcional e versionada, nunca verdade universal.

Fontes oficiais:

- [Regulamentos FIA WEC](https://www.fia.com/regulation/category/706)
- [Regulamento esportivo WEC 2026](https://www.fia.com/system/files/documents/2026_fia_world_endurance_championship_sporting_regulations_clean_v1.2wmsc.pdf)

## SteamVR e OpenXR

### SteamVR — completar antes de expandir

OpenVR oferece exatamente o modelo necessário: overlays 2D sobre qualquer cena,
transform absoluto ou relativo ao dispositivo, textura, alpha e eventos de
mouse/controlador.

Backlog:

- transforms head, cockpit/world e dashboard;
- editor dentro do headset;
- input por controller com modo explícito para evitar missclick;
- escala/curvatura/distância por painel;
- reconnect após SteamVR reiniciar;
- validação 90/120 Hz e headset sleep/wake;
- atlas/texturas reutilizadas, sem duplicar lógica de widget;
- configuração desktop continua disponível como fallback.

Fontes Valve:

- [OpenVR API](https://github.com/ValveSoftware/openvr/wiki/API-Documentation)
- [IVROverlay](https://github.com/ValveSoftware/openvr/wiki/IVROverlay_Overview)

### OpenXR — manter como experimento isolado

OpenXR core permite composition layers pertencentes à própria sessão. A extensão
`XR_EXTX_overlay` existe, mas não é uma extensão ratificada e sua disponibilidade
depende do runtime. Portanto um “adaptador OpenXR universal” não pode ser
prometido agora.

- [OpenXR registry](https://registry.khronos.org/OpenXR/)
- [XR_EXTX_overlay](https://registry.khronos.org/OpenXR/specs/1.0/man/html/XR_EXTX_overlay.html)

Decisão: terminar SteamVR; depois criar prova de conceito OpenXR atrás de uma
interface, detectando a extensão em runtime e falhando sem afetar desktop/VR.

## EAC e segurança

A arquitetura atual minimiza a superfície de conflito, mas não equivale a uma
certificação da Easy Anti-Cheat. Não foi encontrada uma garantia pública da Epic
ou Studio 397 para este executável específico.

Manter como invariantes:

- somente mapping oficial com direito de leitura;
- HTTP apenas em `127.0.0.1`, GET e endpoints observados da UI oficial;
- nenhuma enumeração/leitura da memória privada do processo;
- nenhuma injeção, hook gráfico, driver, automação ou escrita no jogo;
- VR somente pelo compositor SteamVR;
- log de segurança mostrando quais adaptadores estão ativos;
- feature flags para desativar HTTP e VR de forma independente.

Antes de declarar uma versão estável, solicitar confirmação formal à Studio 397
sobre o uso externo do mapping e do endpoint local. A documentação pública da
Epic explica o objetivo preventivo do EAC, mas não publica seus critérios de
detecção, portanto suposições sobre allowlist não devem virar promessa de
produto:

- [Epic Online Services Trust & Safety](https://onlineservices.epicgames.com/trust-safety)
- [Termos do serviço Anti-Cheat](https://onlineservices.epicgames.com/services/terms/agreements?lang=en-US)

## UX visual sem abandonar o padrão atual

O visual RedFox deve permanecer. As melhorias são sistêmicas:

- uma grade tipográfica por distância de leitura, não por widget;
- densidade compacta/normal por perfil;
- largura fixa para números, tempos e gaps para eliminar jitter;
- thresholds com hysteresis para cores de pneus/energia;
- texto/ícone/forma além da cor para flag, temperatura e alerta;
- safe areas e presets por 1080p, 1440p, 4K, ultrawide e VR;
- undo/redo e backup automático antes de migrar layouts;
- navegação por teclado na configuração e tooltips de origem/unidade;
- português/inglês como primeira etapa de localização.

Como referência objetiva, WCAG 2.2 recomenda contraste mínimo de 4.5:1 para
texto normal, 3:1 para texto grande e não usar cor como único meio de transmitir
informação:

- [Contraste mínimo](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum)
- [Uso de cor](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color)

WCAG é uma referência, não uma alegação de conformidade formal do overlay.

## Distribuição e confiança

### Lacunas

- assinatura é opcional e o release atual pode ser não assinado;
- cada versão não assinada recomeça reputação por hash no SmartScreen;
- workflow produz checksum, mas não atesta origem do binário;
- soak longo é manual;
- falta SBOM e processo de rollback/migração de perfil entre versões.

### Próximo release pipeline

1. Build somente no GitHub Actions a partir da tag protegida.
2. Testes + replay suite + soak estendido em job agendado.
3. Gerar SBOM SPDX ou CycloneDX.
4. Gerar attestation de ZIP, manifesto e SBOM.
5. Assinar EXE/DLL com identidade consistente e timestamp.
6. Verificar assinatura, SHA e attestation antes de publicar.
7. Publicar notas com versão LMU/header validada e matriz de cenários.
8. Manter ZIP anterior e migrador reversível de layout.

Microsoft informa que uma assinatura consistente permite acumular reputação do
publisher; sem assinatura, cada novo hash começa do zero. O GitHub permite
attestations de provenance e SBOM em repositórios públicos:

- [SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- [GitHub artifact attestations](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations)

## Matriz mínima de validação

Cada cenário deve registrar versão do jogo, hashes dos headers, resolução, DPI,
renderer, taxa do monitor, classe/carro e duração.

| Grupo | Cenários obrigatórios |
|---|---|
| Ciclo | jogo fechado, menu, load, realtime, unload, fechar jogo, reconnect |
| Sessão | practice, qualifying, race por volta, race por tempo, replay |
| Volta | outlap, primeira válida, invalidada, pit in/out, linha de chegada |
| Campo | 1 carro, grid cheio, multiclass, troca de piloto, disconnect de rival |
| Clima | dry, wet, transição, mudança de grip, noite/dia |
| Pit | request, entry, stopped, refuel, pneus, exit, rival em pit |
| Tela | 1080p, 1440p, 4K, ultrawide, 100–200% DPI, dois monitores |
| Sistema | alt-tab, sleep/wake, mudança de resolução, device reset |
| VR | SteamVR restart, HMD sleep, 90/120 Hz, input/editor |
| Endurance | 1 h, 3 h e 6 h com contagem de memória/handles/latência |

Fixtures devem ser anonimizadas e conter sequências, não apenas um snapshot:
event counter, timestamp, scoring, telemetria do jogador e metadados mínimos dos
rivais. Dados de nomes/IDs devem ser substituídos de forma determinística.

## Backlog priorizado

### P0 — antes de mais features

1. Aquisição híbrida por `LMU_Data_Event` + contadores, métricas end-to-end.
2. Replay recorder/player anonimizado e fixtures sequenciais.
3. Device-loss recovery e Per-Monitor DPI v2 nos dois renderers nativos.
4. Schema guard/circuit breaker do endpoint de optimal.
5. Matriz live para dashboard, Relative, standings e sessão.

Critério de saída: nenhuma fila acumulada; dashboard latest-only; reconexão sem
reiniciar; p99 e stale age registrados; perfis não mudam ao travar o overlay.

### P1 — confiabilidade de produto

1. Estratégia 2.1 determinística, robusta e com pit windows completos.
2. Catálogo data-driven de carros/números/thresholds.
3. Assinatura obrigatória, SBOM e GitHub attestation.
4. Soak automatizado longo e leak/device reset tests.
5. SteamVR interaction/transforms/performance.

### P2 — expansão controlada

1. Monte Carlo/scenarios da estratégia.
2. Localização PT-BR/EN.
3. Configuração de colunas/densidade e acessibilidade.
4. OpenXR proof of concept condicionado a suporte real do runtime.
5. Novos widgets somente com campo oficial e caso de uso validado.

## O que não fazer agora

- Não substituir todo WPF: manter editor/configuração/fallback.
- Não voltar a hook/injeção DirectX no jogo.
- Não fazer múltiplos requests HTTP por frame; HTTP não pertence ao hot path.
- Não renderizar standings a 240 Hz sem scoring novo.
- Não apresentar estimativa derivada como valor oficial.
- Não prometer compatibilidade EAC certificada sem confirmação do fornecedor.
- Não começar OpenXR antes de completar e medir SteamVR.
- Não usar IA/RL na estratégia antes de dados, calibração e baseline explicável.

## Sequência recomendada de construção

O próximo ciclo deve durar o necessário para fechar P0, nesta ordem:

1. instrumentar contador/evento e latency trace sem alterar a UI;
2. implementar event-driven hybrid reader com feature flag e comparar A/B;
3. criar recorder/replay e fixtures de uma sessão real;
4. implementar device-loss/DPI recovery;
5. executar a matriz curta e um soak de uma hora;
6. só então iniciar Strategy 2.1 e SteamVR interaction em paralelo de roadmap.

Essa sequência melhora velocidade percebida, confiança nos valores e capacidade
de detectar regressões sem aumentar o risco com EAC.
