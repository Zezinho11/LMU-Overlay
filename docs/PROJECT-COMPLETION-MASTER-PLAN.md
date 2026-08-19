# LMU Overlay — Especificação mestre e plano de conclusão

> Fonte de verdade consolidada em 19 de agosto de 2026.
>
> Este documento reúne as decisões tomadas durante toda a construção do
> projeto, confrontadas com o estado do código e das releases até a `v0.6.6`.
> Ele substitui o roadmap antigo para o planejamento das próximas etapas, sem
> substituir ADRs, documentação técnica detalhada ou o changelog.

## 1. Objetivo do documento

O propósito deste arquivo é permitir que todas as etapas restantes sejam
implementadas sem reabrir decisões já estabelecidas. Ele registra:

- o produto que estamos construindo;
- limites técnicos e de segurança que não podem ser violados;
- aparência e comportamento esperados de cada overlay;
- regras de dados, timing, estratégia e persistência;
- o que já está concluído no código;
- o que ainda depende de teste ao vivo;
- defeitos e lacunas realmente restantes;
- a ordem obrigatória da implementação;
- critérios objetivos de aceite e de release.

### Legenda de estado

- **CONCLUÍDO:** implementado, coberto por testes automatizados e publicado.
- **IMPLEMENTADO / VALIDAR:** existe no código, mas precisa de validação manual
  ou física antes de ser considerado encerrado.
- **EM CURSO:** existe como alteração local ainda não publicada.
- **PENDENTE:** ainda precisa ser construído.
- **OPCIONAL POSTERIOR:** não bloqueia a conclusão principal do produto.

## 2. Visão definitiva do produto

O LMU Overlay é um conjunto externo, modular e personalizável de instrumentos
para Le Mans Ultimate. Ele deve fornecer ao piloto dados rápidos e confiáveis de
pilotagem, timing multiclass, posição física na pista, estratégia de endurance,
condição de pista e estado do carro.

O produto deve:

1. funcionar em desktop e SteamVR;
2. preservar paridade funcional entre os dois hosts;
3. não injetar código nem interferir no jogo ou no Easy Anti-Cheat;
4. usar prioritariamente a interface oficial de shared memory do LMU;
5. apresentar informações em tempo real conforme a cadência real de cada fonte;
6. permitir que cada usuário monte sua própria experiência visual;
7. manter RedFox Racing como identidade padrão, não como limitação;
8. continuar expansível para novos widgets, OpenXR e outras formas de layout;
9. ser distribuído publicamente pelo GitHub com build reproduzível e verificável.

### Público e uso principal

- pilotos de sim racing e endurance;
- uso pessoal durante Practice, Qualifying e Race;
- preparação de stint e corrida durante Practice;
- uso em monitor, ultrawide, 4K e SteamVR;
- não inclui recursos específicos de streamer neste escopo.

### Fora do escopo atual

- streamer features;
- captura de vídeo ou composição para transmissão;
- automação de input, macros ou controle do carro;
- injeção DirectX dentro do processo do LMU;
- leitura de memória privada do processo;
- promessa de certificação EAC sem confirmação do fornecedor;
- IA/RL para estratégia antes de existir baseline robusto e dados calibrados;
- OpenXR como requisito da versão principal antes de concluir SteamVR.

## 3. Invariantes não negociáveis

### 3.1 Segurança e Easy Anti-Cheat

Toda implementação deve respeitar estes limites:

- abrir `LMU_Data` somente com `MemoryMappedFileRights.Read`;
- abrir `LMU_Data_Event` apenas para sincronização;
- nunca escrever ou sinalizar estruturas pertencentes ao jogo;
- nunca usar `ReadProcessMemory`, `WriteProcessMemory` ou scan de memória;
- nunca injetar DLL, instalar hook gráfico ou interceptar funções do jogo;
- nunca automatizar teclado, mouse, volante ou comandos de pit;
- não modificar arquivos do jogo;
- HTTP permitido somente em `127.0.0.1`, por `GET`, para endpoint observado da
  interface oficial e fora do hot path;
- SteamVR somente por `IVROverlay`, em processo separado;
- qualquer nova integração deve ter desligamento independente;
- a documentação deve dizer que baixo risco não significa aprovação formal do
  EAC ou da Studio 397.

### 3.2 Paridade Desktop / VR

Tudo criado daqui em diante deve ser implementado simultaneamente para desktop
e SteamVR, salvo mecânicas exclusivas do host:

- desktop: click-through, janela do LMU, mouse e DPI;
- VR: metros, transform, compositor e controller.

Dados, regras, temas, módulos, localização, alertas e estratégia precisam ser
idênticos. O teste `LmuOverlay.PresentationParity.Tests` deve continuar sendo um
gate obrigatório.

### 3.3 Tempo real sem fila acumulada

- Dashboard e Inputs consomem sempre o snapshot mais recente.
- RPM, marcha, shift lights, pedais e direção não podem aguardar HTTP.
- Nenhuma fila de frames antigos pode ser renderizada depois.
- Telemetria rápida atualiza na cadência da telemetria do jogo.
- Standings e Relative atualizam somente quando scoring novo existe.
- Estratégia, clima e sessão usam cadência menor compatível com a fonte.
- Perder um frame é preferível a acumular atraso.
- Uma falha de renderer não pode parar a captura.

### 3.4 Fontes e honestidade dos dados

- Campos oficiais têm prioridade.
- Estimativas precisam ser internamente rotuladas como derivadas.
- Valor stale deve ser neutralizado, não reapresentado como atual.
- Se não existir referência válida, mostrar `--`, `NEW` ou indisponível.
- Nunca inventar número de carro, gap, setor, pit state ou energia.
- Regras reais do WEC não substituem as regras efetivamente aplicadas pelo LMU.

### 3.5 Regras específicas de sim racing

- Bandeira amarela no LMU não implica Safety Car.
- Não assumir redução de velocidade em yellow flag.
- Não descontar pit loss por yellow flag.
- Flag é alerta de incidente e não gatilho automático de estratégia de SC.
- Clima deve ser apresentado como estado/cenário, não previsão garantida.

## 4. Arquitetura estabelecida

```text
LMU shared memory oficial + evento de atualização
                      |
            adapter somente leitura
                      |
          snapshots imutáveis normalizados
                      |
        regras compartilhadas de widgets
              /                    \
 Desktop WPF/editor + DirectX     SteamVR IVROverlay
```

### Responsabilidades

- **LmuSharedMemory:** leitura coerente e parsing da API.
- **Domain:** modelo normalizado independente do renderer.
- **Core:** runtime, latest-frame, métricas, recording e replay.
- **Widgets:** regras de apresentação, cálculo e estado compartilhado.
- **DirectX:** surfaces nativas de baixa latência no desktop.
- **Desktop:** editor, toolbar, perfis, fallback WPF e integração de janelas.
- **SteamVR:** compositor, texturas, transforms e editor VR no desktop.

### Regra sobre WPF e DirectX

WPF deve continuar sendo usado para configuração, edição e fallback. Ele não
deve voltar a ser o renderer principal de Dashboard/Inputs/Timing durante a
pilotagem. A solução do gargalo foi mover o hot path para DirectComposition;
essa decisão permanece.

### Regra sobre HTTP

Não serão feitos múltiplos requests HTTP por segundo em vários endpoints. A
shared memory é a fonte principal. O endpoint local de Timing é exceção isolada
para reproduzir o `optimal` oficial e deve operar com timeout curto, cache,
schema guard, circuit breaker e feature flag.

## 5. Estado consolidado do produto

### 5.1 Concluído e publicado até `v0.6.6`

- fundação .NET, domínio, parser, probe e CI;
- leitura coerente somente leitura do mapping oficial;
- captura dirigida por `LMU_Data_Event`, com timeout de recuperação;
- latest-frame runtime, reconexão e métricas de saúde;
- renderer Direct3D 11/Direct2D/DirectComposition para Dashboard e Inputs;
- renderer nativo para Live Standings e Relative;
- fallback WPF e modo de edição;
- overlays movíveis, redimensionáveis, ocultáveis e com opacidade própria;
- posições normalizadas, snapping, lock e click-through;
- toolbar flutuante para editar, travar, configurar e trocar perfil;
- perfis criáveis, duplicáveis, renomeáveis, importáveis e exportáveis;
- Dashboard RedFox completa;
- Inputs com volante transparente girando conforme o jogador;
- Live Standings multiclass;
- Relative baseado em posição física na pista;
- Fuel & Virtual Energy com estratégia Full Push e Fuel Save;
- Session/Weather/Grip/Flags;
- Race Control, penalidades e danos;
- alertas prioritários;
- persistência local de PB, setores e referências por pista/carro/piloto;
- `optimal` da aba Timing com limpeza na mudança de sessão/pista;
- localização PT-BR/EN;
- replay e gravação anonimizada;
- SteamVR com todos os widgets desktop e reconexão;
- paridade automática Desktop/VR;
- device recovery com backoff;
- testes, soak gates, SBOM e attestation;
- releases públicas portáteis no GitHub;
- ícone do executável RedFox Racing.

### 5.2 Alterações locais em curso

Na branch `agent/customization-system`, ainda sem commit/release:

- correção das cores que eram salvas mas não atingiam todos os renderizadores;
- ativação automática do tema `Custom` ao editar uma cor válida;
- paleta semântica completa: background, card, accent, primary/secondary text,
  information, attention, critical e positive;
- aplicação da paleta no WPF, DirectX e SteamVR;
- aplicação da paleta em estados dinâmicos e gráfico de pedais;
- composição inicial da Dashboard, podendo ocultar Setores, Pneus e Telemetria;
- schema de perfil 21;
- testes de persistência e paridade da paleta/composição;
- documentação e changelog locais.

Essas mudanças compilaram sem warnings e as sete suítes passaram. Elas são o
ponto inicial obrigatório do próximo ciclo.

## 6. Contrato definitivo de cada overlay

## 6.1 Dashboard

### Direção visual

- inspirada na composição das dashboards Porsche/AFX fornecidas como referência;
- bezel escuro e pouco transparente para cobrir a dashboard original do carro;
- título padrão `REDFOX RACING`;
- hierarquia visual clara, sem fontes pequenas demais;
- proporções estáveis em qualquer redimensionamento;
- números tabulares para evitar jitter;
- shift lights no topo e indicadores laterais;
- aparência personalizável sem perder legibilidade.

### Informações obrigatórias

- pista e sessão;
- posição;
- volta igual à indicada pelo HUD do jogo;
- velocidade;
- marcha;
- RPM;
- shift lights em tempo real;
- delta;
- current, last, best e optimal;
- combustível;
- Virtual Energy;
- brake bias;
- TC, Slip, Cut e ABS, com valor e estado de atuação quando disponível;
- temperaturas de óleo e água;
- pit limiter claramente visível quando ativo;
- setores atuais e referências;
- temperaturas e desgaste dos quatro pneus;
- composto;
- throttle, brake, GX e GY;
- gráfico de acelerador e freio com escala fixa.

### Pneus

- temperatura deve corresponder ao valor da aba Tyres do MFD;
- cada pneu tem ícone próprio ao lado de temperatura e desgaste;
- ícone maior que o texto, mantendo equilíbrio visual;
- cor do ícone muda com a temperatura usando hysteresis;
- desgaste exibido como vida restante de forma inequívoca;
- não confundir percentual de vida com percentual já gasto;
- thresholds futuros podem variar por carro/composto via catálogo.

### Setores, Best, PB e Optimal

- somente voltas e setores válidos alimentam referências oficiais;
- nenhum setor de volta inválida pode alimentar PB ou optimal;
- `Best` salvo precisa ser uma volta completa válida;
- PB é salvo localmente por pista/layout, piloto e modelo do carro;
- ao bater PB válido, lap time e setores da volta são salvos atomicamente;
- recordes pessoais de S1/S2/S3 podem ser atualizados separadamente apenas
  quando o setor válido for mais rápido que o recorde existente;
- setor mais lento nunca sobrescreve setor mais rápido já salvo;
- setores atuais aparecem conforme cada setor da volta é concluído;
- referências PB aparecem em roxo; valores da volta atual, em branco;
- delta do setor concluído aparece temporariamente e depois retorna ao PB;
- S1 da outlap não é usado quando contaminado pela saída do pit;
- S2 e S3 da outlap só podem ser referência provisória se o segmento inteiro
  ocorreu fora do pit;
- sem referência honesta, exibir `NEW`;
- ordem S1/S2/S3 não pode ser trocada;
- split cumulativo do LMU deve ser decomposto corretamente;
- `optimal` oficial é limpo em toda mudança de pista ou sessão.

### Telemetria e fluidez

- RPM, marcha, shift lights, pedais e gráfico devem acompanhar o LMU sem delay
  perceptível crescente;
- gráfico não pode mudar de escala, proporção ou espessura durante a volta;
- buffers são limitados e reutilizados;
- nenhum input recebido pode desacelerar progressivamente a dashboard;
- stale age e p99 devem estar disponíveis no diagnóstico.

## 6.2 Inputs

- volante real recortado em PNG transparente;
- rotação proporcional ao steering do jogador;
- throttle e brake em tempo real;
- aparência consistente com o tema;
- superfície independente, movível e redimensionável;
- mesmo comportamento no VR.

## 6.3 Live Standings

### Estrutura

- torre vertical e compacta, sem grandes barras pretas laterais;
- aparência mais quadrada, preservando o estilo RedFox já aprovado;
- cabeçalho mostra tipo da sessão e tempo restante;
- Relative não herda esse cabeçalho;
- cabeçalho de classe aparece somente na faixa de categoria;
- não repetir `REDFOX` ou `GT3` fora do local correto.

### Linhas

- posição na classe;
- fabricante por código de três letras e cor estável (`BMW`, `FER`, `AST`,
  `POR`, `FOR`, `LEX`, `MCL`, etc.);
- número real do carro;
- abreviação do piloto;
- volta relevante;
- interval/gap;
- pit state;
- composto de pneu;
- percentual de Virtual Energy do próprio carro.

### Seleção multiclass

- classe do jogador recebe o maior número possível de linhas dentro do espaço;
- P1 da classe do jogador permanece fixo;
- uma janela dinâmica acompanha o jogador à frente e atrás;
- quando existe espaço, preencher com mais carros da classe do jogador;
- para cada outra classe, mostrar somente o P1 compacto;
- a regra de composto/Virtual Energy também vale para P1 das outras classes;
- máximo configurável sem ultrapassar a capacidade visual.

### Regras por sessão

- Race: interval conforme corrida, pit e voltas de diferença;
- Qualifying: coluna interval mostra diferença entre melhores voltas da classe;
- piloto sem volta válida mostra indisponível;
- Practice: mantém session clock e timing coerente;
- nenhuma linha stale pode conservar energia/composto antigo como atual.

## 6.4 Relative

- mesma proporção geral do Live Standings;
- sem sessão ou relógio no topo;
- ordem baseada na posição física circular na pista, não na classificação;
- jogador sempre no centro visual quando possível;
- linha mostra posição geral de corrida, classe, piloto e gap físico;
- não usar número do carro como primeiro número;
- o carro P14 fisicamente à frente do jogador P8 deve aparecer à frente;
- pit state explícito;
- gaps oficiais ahead/behind usados quando válidos;
- demais gaps devem evoluir para interpolação entre amostras de scoring;
- transição start/finish não pode inverter ou saltar linhas;
- baixa velocidade e pit lane exigem clamp/freeze controlado;
- origem do gap deve ser rastreável internamente: `official`, `interpolated` ou
  `derived`.

## 6.5 Fuel & Virtual Energy / Estratégia

### Dados básicos

- combustível atual;
- consumo por volta;
- autonomia em voltas e tempo;
- Virtual Energy atual;
- uso de VE por volta;
- autonomia de VE;
- voltas/tempo até o fim;
- combustível/VE necessário e margem;
- próxima parada, combustível a adicionar e confiança.

### Plano Full Push

- primeiro stint usa o que já existe no carro;
- stints intermediários são cheios ou quase cheios para minimizar paradas;
- última parada adiciona somente o necessário para o stint final + reserva de
  uma ou duas voltas configurada;
- nunca encher o tanque na última parada quando restar apenas stint curto;
- VE final recebe alvo equivalente;
- Practice calcula plano completo para preparar o piloto;
- duração exibida em sessão cronometrada é exatamente o tempo restante, sem
  acumular degradação de pace até criar horas fictícias.

### Plano Fuel Save

- segunda caixa independente da estratégia normal;
- exibe target de consumo por volta;
- tenta estender stint ou remover uma parada;
- inclui estratégia de pneus correspondente;
- inclui alvo de VE quando possível;
- não renomear cenário de clima/flag como fuel save;
- saving máximo configurável/seguro e premissas visíveis.

### Pneus e pit service

- 85% de vida restante não é motivo automático para trocar;
- projeção exige tendência de desgaste estável e múltiplas amostras;
- respeitar sets disponíveis;
- permitir double/triple stint quando viável;
- avaliar troca parcial se o LMU/evento realmente permitir e houver dados;
- modelar o serviço do LMU na ordem/duração aplicada pelo jogo;
- diferenciar tempo de abastecimento e pneus quando necessário.

### Estratégia futura obrigatória

- separar voltas push/normal/save/outlap/inlap/pit/wet/invalid;
- somente amostras comparáveis treinam o modelo;
- usar estatística robusta (mediana/MAD ou equivalente);
- tratar Fuel e Virtual Energy como restrições independentes;
- enumerar pit windows completos;
- dividir pit loss em entrada, lane, stop e saída;
- gerar planos baseline/full push, fuel save e safe;
- mostrar premissas, margem e motivo de indisponibilidade;
- recalcular incrementalmente por volta/evento;
- cenários de clima e tráfego não podem fingir certeza;
- Monte Carlo somente fora do hot path e depois do determinístico estar validado.

## 6.6 Session / Weather / Grip / Flags

- grip preserva níveis como green, low, medium, high/saturated;
- cada nível tem cor consistente;
- temperatura do ar e pista seguem o visual dos outros módulos;
- clima mostra ícone coerente: sol, nuvens, chuva e intensidade;
- chuva e wetness são exibidos separadamente;
- flag continua escrita (`GREEN`, `YELLOW`, `RED`) e possui cartão da cor;
- ícone, texto e forma acompanham a cor para acessibilidade;
- yellow não altera automaticamente a estratégia de pit.

## 6.7 Race Control / Damage / Alertas

- penalidades, pit status, flag, danos e sistemas;
- alerta crítico tem precedência sobre informação;
- não cobrir permanentemente a visão;
- cores semânticas personalizáveis, mas significado preservado por texto/ícone;
- mesma regra de prioridade no desktop e VR.

## 7. Customização: visão final

Customização não significa somente trocar duas cores. O usuário deve poder
adaptar toda a experiência sem editar código.

### 7.1 Concluído ou em curso

- mover e redimensionar cada overlay;
- escolher canto/posição livre;
- esconder overlays;
- ajustar opacidade e escala;
- perfis nomeados;
- presets;
- lock explícito para impedir missclick;
- tema RedFox, Black, High Contrast, Color Vision Safe e Custom;
- título da Dashboard;
- escala tipográfica por grupo;
- densidade e quantidade de linhas;
- paleta semântica completa (alteração local em curso);
- mostrar/ocultar Setores, Pneus e Telemetria (alteração local em curso).

### 7.2 Resultado final obrigatório

O editor deve permitir:

- preview ao vivo;
- color picker visual além do hexadecimal;
- editar paleta global e override por widget;
- escolher fonte entre uma lista segura empacotada/suportada;
- ajustar tamanho, peso e alinhamento de grupos de texto;
- trocar textos de marca/títulos permitidos;
- configurar visibilidade de módulos e campos;
- rearranjar módulos internos da Dashboard por drag-and-drop;
- redimensionar módulos internos dentro de uma grade;
- escolher layouts `Classic Porsche`, `AFX`, `Minimal`, `Endurance` e `Custom`;
- configurar colunas do Live Standings;
- configurar densidade/linhas do Relative;
- undo/redo;
- restaurar somente um widget ou o perfil completo;
- backup automático antes de migração;
- importar/exportar tema separadamente do posicionamento;
- preview separado de Desktop e VR usando a mesma definição lógica;
- validação de contraste e aviso quando uma cor comprometer leitura;
- teclado para nudge e acessibilidade da janela de configuração.

### 7.3 Modelo técnico esperado

- definição renderer-neutral e versionada;
- `DashboardLayoutDefinition` com grid, módulos, ordem, spans e visibilidade;
- `WidgetStyleOverride` opcional por widget;
- tokens semânticos, nunca brushes WPF persistidos;
- migração reversível e fail-closed;
- limits para impedir layout impossível;
- hosts adaptam a mesma definição às próprias coordenadas;
- VR mantém transforms próprios, não coordenadas de pixels do desktop.

## 8. Pendências técnicas e de produto

## P0 — fechar trabalho local e proteger a base

### P0.1 Finalizar customização em curso

**Estado:** EM CURSO.

- revisar visualmente todas as superfícies com uma paleta extrema de teste;
- validar aplicação no lock/unlock e troca de perfil;
- garantir que updates ao vivo não restaurem cores fixas;
- testar tema claro e contraste;
- testar migração schema 20 → 21;
- criar screenshots baseline;
- commit, push, merge e release hotfix/minor apropriado.

**Aceite:** trocar paleta altera todos os overlays desktop e VR; reiniciar e
trocar perfil preserva o resultado; sete suítes e baseline visual passam.

### P0.2 Atualizar documentação desatualizada

**Estado:** PENDENTE.

- README ainda anuncia `0.6.5` apesar de existir `v0.6.6`;
- roadmap ainda marca localização como pendente;
- auditoria de 5 de agosto descreve limitações já resolvidas;
- converter roadmap antigo em histórico e apontar para este documento;
- atualizar quick starts após o novo editor.

**Aceite:** nenhuma documentação pública contradiz a release atual.

### P0.3 Safe mode e kill switches

**Estado:** PENDENTE.

- flag para desativar endpoint HTTP de optimal;
- flag para desativar SteamVR sem afetar desktop;
- safe mode usando apenas shared memory + WPF fallback, se necessário;
- tela/diagnóstico informa adaptadores ativos;
- configuração persistente e argumento CLI de emergência.

**Aceite:** cada integração opcional pode falhar/desligar sem perder o restante.

### P0.4 Endpoint de Optimal robusto

**Estado:** PARCIAL.

- manter GET somente localhost e timeout curto;
- adicionar schema guard versionado;
- fixtures de múltiplas respostas conhecidas;
- circuit breaker com backoff;
- cache do último valor válido somente dentro da mesma sessão;
- limpar imediatamente em nova sessão/pista;
- diagnosticar indisponibilidade sem afetar hot path;
- feature flag do item P0.3.

**Aceite:** endpoint quebrado, lento ou alterado não congela nem contamina a
Dashboard e nunca carrega optimal de outra pista.

## P1 — exatidão e compatibilidade verificável

### P1.1 Catálogo data-driven de carros

**Estado:** PENDENTE.

Criar catálogo versionado por modelo/classe contendo, quando verificado:

- fabricante e código de três letras;
- cor/identidade visual da marca;
- fonte correta do número do carro e regras de fallback;
- RPM máximo e faixa de shift lights;
- TC/Slip/Cut/ABS disponíveis;
- unidades e formatos;
- thresholds de temperatura por categoria/composto quando confirmados.

Heurísticas permanecem apenas como fallback explícito. Adicionar golden fixture
por classe e por fabricantes GT3 relevantes.

**Aceite:** `MFR`/`---` aparece somente quando o carro realmente não está no
catálogo e nunca se usa vehicle ID como número de corrida.

### P1.2 Relative 2.0

**Estado:** PARCIAL.

- manter ordenação por lap distance;
- usar gaps oficiais do vizinho ahead/behind;
- interpolar demais carros entre duas amostras de scoring;
- tratar pit/baixa velocidade/start-finish;
- armazenar origem/confiança do gap;
- neutralizar stale;
- fixtures de tráfego, multiclass e ultrapassagem.

**Aceite:** ordem e gaps batem com o relative do jogo nos cenários da matriz.

### P1.3 Live Standings 2.0

**Estado:** PARCIAL.

- cache por generation + slot + identity;
- invalidar slot reutilizado/troca de piloto;
- timestamps próprios para composto e VE de cada rival;
- catálogo para fabricante/número;
- qualifying sem volta, deleted lap, P1 no pit, reconnect e classe inesperada;
- coluna configurável sem quebrar proporção.

**Aceite:** nenhum rival herda dados do ocupante anterior do slot.

### P1.4 Matriz de compatibilidade LMU

**Estado:** PENDENTE CONTÍNUO.

Registrar por versão do jogo/header:

- hashes e offsets validados;
- probe live;
- Practice/Qualifying/Race;
- lap-limited/time-limited;
- dry/wet/transição;
- grid vazio/cheio/multiclass;
- pit e troca de piloto;
- regressão após atualização do LMU/EAC.

**Aceite:** release informa explicitamente a versão LMU validada.

## P2 — Strategy Engine 2.1

### P2.1 Pipeline de amostras comparáveis

- classificar volta/stint em push, normal, save, outlap, inlap, pit, wet e
  invalid;
- detectar mudança de condição e tráfego;
- rejeitar amostra não comparável;
- usar mediana/MAD e intervalos;
- persistência opcional local de calibração por pista/carro/piloto.

### P2.2 Solver determinístico completo

- enumerar todo pit window viável;
- Fuel e VE independentes;
- pit loss por componentes;
- fuel mass e degradação de pneu;
- pneus disponíveis e multi-stint;
- stint final exato;
- baseline/full push, fuel save e safe;
- regras configuráveis e origem visível;
- cálculo em Practice usando duração correta;
- explicação de por que cada plano foi escolhido.

### P2.3 Calibração ao vivo

- recalcular por volta/evento, nunca por frame;
- comparar previsão com resultado real;
- atualizar confiança;
- registrar decisão/input/resultado anonimizável para replay;
- não bloquear captura ou render.

**Aceite P2:** replays determinísticos cobrem corrida de 1 h, 3 h e 6 h; plano
final não excede sessão; última carga sobra a reserva configurada; nenhuma
regra de Safety Car é aplicada ao yellow.

## P3 — Strategy Engine 2.2 por cenários

**Estado:** PENDENTE, depois de P2 validado.

- cenários de clima sem alegar previsão oficial;
- tráfego e posição provável de saída;
- distribuições de pace, consumo, pit loss e degradação;
- Monte Carlo com orçamento fixo em worker isolado;
- probabilidade de chegar, evitar splash e sair em ar limpo;
- apresentar faixas/probabilidade, não falsa precisão;
- respostas de rivais somente com evidência suficiente.

## P4 — Editor visual completo

**Estado:** PENDENTE após estabilizar a paleta em curso.

### P4.1 Definição de layout interno

- schema de módulos e grid;
- presets equivalentes ao layout atual;
- migração sem mudança visual para usuários existentes;
- adapters Desktop/VR.

### P4.2 Editor drag-and-drop

- selecionar, mover, redimensionar e ordenar módulos;
- propriedades de módulo e texto;
- preview em tempo real;
- undo/redo e reset parcial;
- keyboard nudge;
- proteção contra overlap e layout impossível.

### P4.3 Customização das torres e outros widgets

- colunas e densidade do Live Standings;
- campos e quantidade do Relative;
- estilos dos cartões Session/Fuel/Race Control;
- overrides por widget;
- compartilhamento de tema/layout entre usuários.

**Aceite P4:** usuário consegue reproduzir o layout padrão e criar uma dashboard
diferente sem editar JSON ou código; o mesmo perfil lógico funciona em VR.

## P5 — Renderer, DPI e hardware

### P5.1 Per-Monitor DPI v2 completo

**Estado:** PENDENTE.

- manifest/awareness correto;
- tratar `WM_DPICHANGED` nas janelas nativas;
- Direct2D no DPI real, não fixo 96;
- recriar texto/surface ao mudar monitor;
- validar 100/125/150/200% e dois monitores.

### P5.2 Pacing e métricas end-to-end

**Estado:** PARCIAL.

- medir produtor → snapshot → publish → present;
- p50/p95/p99/max por janela;
- taxa de evento, frames novos/duplicados/descartados;
- avaliar frame-latency waitable object contra `Present(1)` no hardware real;
- não habilitar tearing sem evidência.

### P5.3 Recuperação e long run

**Estado:** IMPLEMENTADO / VALIDAR.

- device-loss recovery já existe;
- validar driver reset, alt-tab, sleep/wake e troca de resolução;
- soak de 1 h, 3 h e 6 h;
- medir working set, handles, CPU, GDI/USER e recursos D3D.

**Aceite P5:** sem crescimento não limitado, sem atraso acumulado e recuperação
sem reiniciar o app.

## P6 — SteamVR release qualification

### P6.1 Validação física

**Estado:** PENDENTE.

- headset real em 90 Hz e 120 Hz;
- sleep/wake do HMD;
- restart do SteamVR;
- sessões longas;
- legibilidade, tamanho métrico e conforto;
- performance simultânea desktop + VR;
- todas as superfícies e perfis.

### P6.2 Interação no headset

**Estado:** PENDENTE.

- modo de edição explícito para controller;
- ray/pointer e confirmação;
- lock para evitar missclick;
- mover, distância, escala e opacidade no headset;
- transforms head, cockpit/world e dashboard quando suportados;
- curvatura somente com validação visual/performance;
- editor desktop permanece fallback.

### P6.3 OpenXR

**Estado:** OPCIONAL POSTERIOR.

- criar interface de compositor desacoplada;
- PoC isolada e feature-flagged;
- detectar extensão em runtime;
- falhar sem afetar Desktop ou SteamVR;
- não anunciar suporte universal se o runtime não suportar overlay.

## P7 — distribuição, segurança e operação

### P7.1 Assinatura de código

**Estado:** PENDENTE.

- obter identidade/certificado de publisher;
- Authenticode obrigatório para release estável;
- timestamp;
- verificar assinatura no workflow;
- manter checksum, SBOM e attestation existentes.

### P7.2 Atualização e rollback

**Estado:** PARCIAL.

- `latest.json` já existe;
- implementar checagem opcional de update sem autoexecutar código baixado;
- mostrar release notes e link oficial;
- verificar SHA/attestation;
- backup de perfil antes de migração;
- rollback de perfil e manutenção da versão anterior do ZIP.

### P7.3 Segurança contínua

- revisão de dependências;
- CI procurando APIs proibidas;
- documentação de adaptadores ativos;
- processo de resposta a incompatibilidade LMU/EAC;
- tentar confirmação formal da Studio 397;
- nunca transformar ausência de problema em promessa de allowlist.

## P8 — novos widgets e crescimento

**Estado:** somente depois de P0–P6 essenciais.

- reauditar funcionalidades desejadas originalmente em comparação com o
  produto de referência, excluindo streamer features;
- cada novo widget exige campo oficial verificado e caso de uso real;
- implementar Desktop e VR na mesma mudança;
- evitar duplicação de informação já existente;
- manter overlays em caixas independentes;
- preservar espaço arquitetural para extensões futuras.

Possíveis áreas, somente após verificação oficial:

- stint history detalhado;
- pit window/traffic visualization;
- pneus disponíveis por evento;
- comparação de stint/piloto;
- alertas configuráveis por regra;
- telemetry review pós-stint fora do hot path.

## 9. Plano de execução obrigatório

### Etapa 1 — publicar a customização atual

1. testes visuais manuais;
2. corrigir qualquer cor fixa restante;
3. testar migração e troca de perfil;
4. screenshots baseline;
5. commit e release.

### Etapa 2 — confiabilidade P0

1. safe mode/kill switches;
2. optimal schema guard/circuit breaker;
3. documentação consolidada;
4. fixtures e testes de falha.

### Etapa 3 — exatidão de timing

1. catálogo de carros;
2. Relative 2.0;
3. Live Standings 2.0;
4. matriz live curta.

### Etapa 4 — Strategy 2.1

1. amostras classificadas;
2. estatística robusta;
3. solver completo;
4. planos explicáveis;
5. replays 1/3/6 h.

### Etapa 5 — editor visual completo

1. schema renderer-neutral;
2. migração do layout atual;
3. editor interno da Dashboard;
4. torres/outros widgets;
5. paridade VR.

### Etapa 6 — hardware e qualificação

1. Per-Monitor DPI v2;
2. pacing e métricas;
3. soak longo;
4. matriz desktop;
5. headset 90/120 Hz;
6. controller editor.

### Etapa 7 — release estável

1. assinatura;
2. update/rollback;
3. security review;
4. compatibilidade LMU/EAC documentada;
5. release candidate;
6. release estável.

### Etapa 8 — expansão posterior

1. Strategy 2.2/Monte Carlo;
2. novos widgets verificados;
3. OpenXR PoC;
4. outras extensões sem violar os invariantes.

## 10. Matriz mínima de validação

| Grupo | Cenários obrigatórios |
|---|---|
| Ciclo | jogo fechado, menu, load, sessão, unload, fechar e reconnect |
| Sessão | Practice, Qualifying, Race por volta, Race por tempo e replay |
| Volta | outlap, primeira válida, invalidada, pit in/out e start/finish |
| Timing | sem volta, deleted lap, troca de piloto, P1 no pit e reconnect |
| Campo | um carro, grid cheio, multiclass e slot reutilizado |
| Clima | dry, wet, transição, grip e noite/dia |
| Pit | request, entry, stopped, fuel, pneus, exit e rival em pit |
| Tela | 720p, 1080p, 1440p, 4K, ultrawide e redimensionamento extremo |
| DPI | 100%, 125%, 150%, 200% e dois monitores |
| Sistema | alt-tab, sleep/wake, resolução e device reset |
| VR | restart, sleep, 90/120 Hz, editor e controller |
| Endurance | 1 h, 3 h e 6 h com memória, handles, CPU e p99 |
| Perfil | criar, duplicar, importar, migrar, lock e trocar resolução |
| Tema | escuro, claro, alto contraste e paleta extrema |

## 11. Critério de pronto para qualquer mudança

Uma etapa só pode ser marcada como concluída quando:

1. mantém o boundary EAC;
2. usa fonte oficial ou declara estimativa;
3. Desktop e VR têm paridade quando aplicável;
4. não introduz fila ou atraso crescente;
5. release build compila com zero warnings;
6. testes automatizados relevantes passam;
7. replay cobre o comportamento;
8. teste ao vivo proporcional ao risco foi executado;
9. perfil antigo migra ou falha de forma recuperável;
10. documentação e changelog foram atualizados;
11. UI funciona nas resoluções e escalas suportadas;
12. erros deixam o restante do overlay funcionando;
13. nenhuma informação inválida/stale é apresentada como oficial.

## 12. Critério de conclusão do projeto principal

O projeto principal estará completo quando:

- customização completa estiver publicada e validada;
- timing e catálogos tiverem exatidão comprovada ao vivo;
- Strategy 2.1 estiver validada em corridas longas;
- editor visual permitir composição real sem JSON/código;
- DPI e long-run estiverem qualificados;
- SteamVR estiver testado fisicamente em 90/120 Hz;
- safe mode e kill switches existirem;
- releases estáveis estiverem assinadas/verificáveis;
- matriz LMU/EAC estiver documentada;
- não houver defeito P0/P1 aberto.

Strategy 2.2, novos widgets e OpenXR permanecem crescimento posterior e não
devem atrasar uma versão principal estável, desde que a arquitetura continue
preparada para recebê-los.

## 13. Próxima ação

A próxima ação após a aprovação deste documento é iniciar a **Etapa 1 — publicar
a customização atual**, partindo da branch `agent/customization-system`.

Não se deve começar um novo widget antes de:

1. validar visualmente a paleta em todas as surfaces;
2. encerrar o schema 21;
3. publicar essa base;
4. implementar os kill switches e robustez do optimal.

Esse encadeamento protege o trabalho já realizado e cria a fundação necessária
para executar todas as demais etapas sem retrabalho de renderer, perfil ou VR.
